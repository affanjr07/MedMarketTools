using System.Diagnostics;

namespace LuaToolsGui.Services;

/// <summary>
/// Opens the app's Discord invite, preferring the native Discord client (deep link) and falling back
/// to the browser (https invite) when Discord isn't installed or the protocol handler fails.
/// </summary>
public static class DiscordLauncher
{
    /// <summary>Open the invite. Returns the URL that was handed to the OS.</summary>
    public static void OpenInvite()
    {
        // Discord's app protocol: discord://-/invite/<code>. Opens the installed client directly.
        string deepLink = $"discord://-/invite/{AppConfig.DiscordInviteCode}";
        if (TryOpen(deepLink)) return;

        TryOpen(AppConfig.DiscordInviteUrl);
    }

    private static bool TryOpen(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch
        {
            return false; // no handler registered (e.g. Discord not installed) — caller falls back
        }
    }
}
