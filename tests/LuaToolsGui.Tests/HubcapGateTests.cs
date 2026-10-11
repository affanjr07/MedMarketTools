using LuaToolsGui.Services;
using Xunit;

namespace LuaToolsGui.Tests;

/// <summary>
/// The "access" gate for adding games is a valid-format Hubcap key (keys can only be saved after
/// being verified against Hubcap in Settings). These pin down the exact format the gate accepts.
/// </summary>
public class HubcapGateTests
{
    private static string Key(char fill = 'a') => "smm_" + new string(fill, 96);

    [Fact]
    public void ValidLowercaseHex_Accepted() =>
        Assert.True(HubcapService.IsValidKeyFormat(Key()));

    [Fact]
    public void ValidDigitHex_Accepted() =>
        Assert.True(HubcapService.IsValidKeyFormat(Key('5')));

    [Fact]
    public void UppercaseHex_Rejected() =>
        Assert.False(HubcapService.IsValidKeyFormat(Key('A')));

    [Fact]
    public void NonHexChar_Rejected() =>
        Assert.False(HubcapService.IsValidKeyFormat(Key('g')));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("smm_")]
    [InlineData("smm_abc")]
    public void InvalidKey_Rejected(string? key) =>
        Assert.False(HubcapService.IsValidKeyFormat(key));

    [Fact]
    public void WrongPrefix_Rejected() =>
        Assert.False(HubcapService.IsValidKeyFormat("abc_" + new string('a', 96)));
}