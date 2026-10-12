using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace LuaToolsGui.Services;

/// <summary>
/// Gates access to "add game" on Discord membership + a required role. The user runs a one-time
/// Discord OAuth2 (authorization-code + PKCE, no client secret) in their browser; we capture the
/// redirect on a localhost listener, exchange the code, then check the Discord API that the account
/// is a member of our guild AND holds the required role. The result is cached so we only ask once.
/// </summary>
/// <remarks>
/// This is a client-side check and is spoofable by design (the user could edit the local cache).
/// That's an accepted trade-off: it keeps the app backend-free. Hardening later means moving the
/// role check server-side.
/// </remarks>
public class DiscordGateService
{
    private const string AuthorizeUrl = "https://discord.com/oauth2/authorize";
    private const string ApiBase = "https://discord.com/api";
    private const string TokenUrl = "https://discord.com/api/oauth2/token";

    private readonly CacheService _cache;
    private readonly HttpClient _http = AppHttp.Create(TimeSpan.FromSeconds(30));

    public DiscordGateService(CacheService cache) => _cache = cache;

    /// <summary>True when the user has completed verification and holds the role (per the cache).</summary>
    public bool IsVerified => _cache.DiscordVerified;

    /// <summary>Discord handle captured at verification time, for display. Null when not verified.</summary>
    public string? Username => _cache.DiscordUsername;

    /// <summary>Raised after a successful verification or a reset, so the UI can refresh.</summary>
    public event Action? Changed;

    /// <summary>Forget the stored verification (e.g. user wants to verify a different account).</summary>
    public void Reset()
    {
        _cache.ClearDiscordVerification();
        Changed?.Invoke();
    }

    /// <summary>
    /// Runs the full browser OAuth2 + role check. Returns normally once verified; throws
    /// <see cref="DiscordGateException"/> with a user-facing message on failure.
    /// </summary>
    public async Task VerifyAsync(CancellationToken ct = default)
    {
        string verifier = CreateCodeVerifier();
        string challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        string state = Base64Url(RandomNumberGenerator.GetBytes(16));

        string url =
            $"{AuthorizeUrl}?client_id={AppConfig.DiscordClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(AppConfig.OAuthCallbackUrl)}" +
            "&response_type=code" +
            "&scope=identify%20guilds%20guilds.members.read" +
            $"&code_challenge={challenge}&code_challenge_method=S256" +
            $"&state={state}";

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{AppConfig.OAuthCallbackPort}/");
        listener.Start();

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        string code;
        try
        {
            (code, string? returnedState) = await WaitForCallbackAsync(listener, ct);
            if (!string.Equals(returnedState, state, StringComparison.Ordinal))
                throw new DiscordGateException(Resources.Strings.Discord_Err_StateMismatch);
        }
        finally
        {
            listener.Stop();
        }

        string accessToken = await ExchangeCodeAsync(code, verifier, ct);

        DiscordUser? user = await GetJsonAsync<DiscordUser>($"{ApiBase}/users/@me", accessToken, ct)
            ?? throw new DiscordGateException(Resources.Strings.Discord_Err_Generic);

        // Must be a member of our guild.
        var guilds = await GetJsonAsync<List<DiscordGuild>>($"{ApiBase}/users/@me/guilds", accessToken, ct) ?? [];
        if (guilds.All(g => g.Id != AppConfig.DiscordGuildId))
            throw new DiscordGateException(Resources.Strings.Discord_Err_NotInServer);

        // ...and must hold the required role.
        var member = await GetJsonAsync<DiscordMember>(
            $"{ApiBase}/users/@me/guilds/{AppConfig.DiscordGuildId}/member", accessToken, ct)
            ?? throw new DiscordGateException(Resources.Strings.Discord_Err_NotInServer);

        if (member.Roles?.Contains(AppConfig.DiscordRequiredRoleId) != true)
            throw new DiscordGateException(Resources.Strings.Discord_Err_NoRole);

        string display = string.IsNullOrWhiteSpace(user.GlobalName) ? user.Username ?? "" : user.GlobalName!;
        _cache.SaveDiscordVerification(user.Id, display);
        Changed?.Invoke();
    }

    // ── OAuth helpers ───────────────────────────────────────────────

    private static async Task<(string Code, string? State)> WaitForCallbackAsync(HttpListener listener, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));

        while (true)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new DiscordGateException(Resources.Strings.Auth_Err_Timeout);
            }

            var query = HttpUtility.ParseQueryString(ctx.Request.Url?.Query ?? "");
            string? code = query.Get("code");
            string? state = query.Get("state");
            string? error = query.Get("error_description") ?? query.Get("error");

            if (code is null && error is null)
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                continue;
            }

            bool ok = code is not null;
            byte[] page = Encoding.UTF8.GetBytes(ResultPage(ok, error));
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.ContentLength64 = page.Length;
            await ctx.Response.OutputStream.WriteAsync(page, timeout.Token);
            ctx.Response.Close();

            if (!ok) throw new DiscordGateException(error ?? Resources.Strings.Discord_Err_Denied);
            return (code!, state);
        }
    }

    private async Task<string> ExchangeCodeAsync(string code, string verifier, CancellationToken ct)
    {
        // Discord's token endpoint expects application/x-www-form-urlencoded (unlike Supabase's JSON).
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = AppConfig.DiscordClientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = AppConfig.OAuthCallbackUrl,
            ["code_verifier"] = verifier,
        });

        var res = await _http.PostAsync(TokenUrl, form, ct);
        string body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new DiscordGateException(string.Format(Resources.Strings.Discord_Err_TokenExchange, (int)res.StatusCode));

        var token = JsonSerializer.Deserialize<DiscordToken>(body);
        if (token is null || string.IsNullOrEmpty(token.AccessToken))
            throw new DiscordGateException(Resources.Strings.Discord_Err_Generic);
        return token.AccessToken;
    }

    private async Task<T?> GetJsonAsync<T>(string url, string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return default;
        string body = await res.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body);
    }

    private static string CreateCodeVerifier() => Base64Url(RandomNumberGenerator.GetBytes(48));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string ResultPage(bool ok, string? error) => $$"""
        <!doctype html>
        <html><head><meta charset="utf-8"><title>MedMarketTools</title>
        <style>
          body { background:#0b0b12; color:#e5e7eb; font-family:'Segoe UI',sans-serif;
                 display:flex; align-items:center; justify-content:center; height:100vh; margin:0; }
          .card { text-align:center; padding:2.5rem 3rem; background:#14141c;
                  border:1px solid rgba(255,255,255,.08); border-radius:14px; }
          h1 { font-size:1.3rem; margin:0 0 .5rem; color:{{(ok ? "#a78bfa" : "#f87171")}}; }
          p { color:#9ca3af; font-size:.95rem; margin:0; }
        </style></head>
        <body><div class="card">
          <h1>{{(ok ? "Verified!" : "Verification failed")}}</h1>
          <p>{{(ok ? "You can close this tab and return to MedMarketTools." : WebUtility.HtmlEncode(error ?? "Please try again from the app."))}}</p>
        </div></body></html>
        """;

    private sealed class DiscordToken
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    }

    private sealed class DiscordUser
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("username")] public string? Username { get; set; }
        [JsonPropertyName("global_name")] public string? GlobalName { get; set; }
    }

    private sealed class DiscordGuild
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
    }

    private sealed class DiscordMember
    {
        [JsonPropertyName("roles")] public List<string>? Roles { get; set; }
    }
}

public class DiscordGateException(string message) : Exception(message);
