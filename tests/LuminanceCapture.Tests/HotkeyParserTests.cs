using LuminanceCapture.Core;

namespace LuminanceCapture.Tests;

public class HotkeyParserTests
{
    [Theory]
    [InlineData("Ctrl+Alt+F9", "Ctrl+Alt+F9")]
    [InlineData("alt+ctrl+f10", "Ctrl+Alt+F10")]
    [InlineData("Win+Shift+G", "Shift+Win+G")]
    [InlineData("Ctrl+Alt+1", "Ctrl+Alt+1")]
    public void ParsesAndNormalizes(string input, string expected)
    {
        Assert.True(HotkeyParser.TryParse(input, out var combo, out _));
        Assert.Equal(expected, combo.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("F9")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Banana")]
    [InlineData("Ctrl++A")]
    [InlineData("Ctrl+LeftShift")]
    public void RejectsInvalid(string input)
    {
        Assert.False(HotkeyParser.TryParse(input, out _, out var error));
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Null_IsRejected() => Assert.False(HotkeyParser.TryParse(null, out _, out _));

    [Fact]
    public void Pair_RejectsSameCombo_EvenIfWrittenDifferently()
    {
        Assert.False(HotkeyParser.ValidatePair("Ctrl+Alt+F9", "alt+ctrl+f9", out var error));
        Assert.Contains("same", error);
    }

    [Fact]
    public void Pair_AcceptsDefaults() =>
        Assert.True(HotkeyParser.ValidatePair(AppSettings.DefaultToggleHotkey, AppSettings.DefaultCaptureHotkey, out _));
}
