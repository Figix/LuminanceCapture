using System.Text.Json;
using LuminanceCapture.Core;

namespace LuminanceCapture.Tests;

public class SettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lc_" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    public SettingsTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, true);

    private AppSettings LoadRaw(string json)
    {
        File.WriteAllText(FilePath, json);
        return new SettingsStore(FilePath).Load();
    }

    [Fact]
    public void Defaults()
    {
        var s = new AppSettings();
        Assert.Equal("Ctrl+Alt+F9", s.ToggleHotkey);
        Assert.Equal("Ctrl+Alt+F10", s.CaptureHotkey);
        Assert.Null(s.SaveFolder);
        Assert.True(s.AutoStart);
        Assert.False(s.CaptureSound);
    }

    [Fact]
    public void DefaultPathIsUnderAppData()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LuminanceCapture", "settings.json");
        Assert.Equal(expected, SettingsStore.DefaultPath);
    }

    [Fact]
    public void RoundTrip()
    {
        var store = new SettingsStore(FilePath);
        var s = new AppSettings
        {
            ToggleHotkey = "Ctrl+Shift+G", CaptureHotkey = "Alt+C",
            SaveFolder = Path.Combine(_dir, "out"), AutoStart = false, CaptureSound = true
        };
        store.Save(s);
        var r = store.Load();
        Assert.Equal(s.ToggleHotkey, r.ToggleHotkey);
        Assert.Equal(s.CaptureHotkey, r.CaptureHotkey);
        Assert.Equal(s.SaveFolder, r.SaveFolder);
        Assert.False(r.AutoStart);
        Assert.True(r.CaptureSound);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void SaveTwiceOverwrites()
    {
        var store = new SettingsStore(FilePath);
        store.Save(new AppSettings { CaptureSound = true });
        store.Save(new AppSettings { CaptureSound = false });
        Assert.False(store.Load().CaptureSound);
    }

    [Fact]
    public void SaveCreatesMissingDirectory()
    {
        var path = Path.Combine(_dir, "sub", "settings.json");
        new SettingsStore(path).Save(new AppSettings());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void MissingFileGivesDefaults()
    {
        var r = new SettingsStore(FilePath).Load();
        Assert.Equal("Ctrl+Alt+F9", r.ToggleHotkey);
        Assert.True(r.AutoStart);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("[1,2]")]
    [InlineData("42")]
    public void CorruptJsonGivesDefaults(string json)
    {
        var r = LoadRaw(json);
        Assert.Equal("Ctrl+Alt+F9", r.ToggleHotkey);
        Assert.Equal("Ctrl+Alt+F10", r.CaptureHotkey);
        Assert.Null(r.SaveFolder);
        Assert.True(r.AutoStart);
        Assert.False(r.CaptureSound);
    }

    [Fact]
    public void PartialInvalidFieldsReplacedOthersKept()
    {
        var folder = Path.Combine(_dir, "keep");
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["toggleHotkey"] = "   ",
            ["captureHotkey"] = "Ctrl+K",
            ["saveFolder"] = folder,
            ["autoStart"] = "yes",
            ["captureSound"] = true,
        });
        var r = LoadRaw(json);
        Assert.Equal("Ctrl+Alt+F9", r.ToggleHotkey);
        Assert.Equal("Ctrl+K", r.CaptureHotkey);
        Assert.Equal(folder, r.SaveFolder);
        Assert.True(r.AutoStart);
        Assert.True(r.CaptureSound);
    }

    [Theory]
    [InlineData("Ctrl+Foo")]
    [InlineData("F9")]
    [InlineData("Ctrl+A+B")]
    public void UnparseableHotkeyFallsBackToDefaultOthersKept(string bad)
    {
        var folder = Path.Combine(_dir, "keep2");
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["toggleHotkey"] = bad,
            ["captureHotkey"] = "Alt+C",
            ["saveFolder"] = folder,
            ["captureSound"] = true,
        });
        var r = LoadRaw(json);
        Assert.Equal("Ctrl+Alt+F9", r.ToggleHotkey);
        Assert.Equal("Alt+C", r.CaptureHotkey);
        Assert.Equal(folder, r.SaveFolder);
        Assert.True(r.CaptureSound);
    }

    [Fact]
    public void IdenticalStoredHotkeysResetCaptureToDefault()
    {
        var r = LoadRaw("{\"toggleHotkey\": \"Ctrl+K\", \"captureHotkey\": \"ctrl+k\"}");
        Assert.Equal("Ctrl+K", r.ToggleHotkey);
        Assert.Equal("Ctrl+Alt+F10", r.CaptureHotkey);
    }

    [Fact]
    public void WrongTypesAndEmptyHotkeys()
    {
        var r = LoadRaw("{\"toggleHotkey\": 5, \"captureHotkey\": \"\", \"saveFolder\": 7, \"autoStart\": false, \"captureSound\": \"x\"}");
        Assert.Equal("Ctrl+Alt+F9", r.ToggleHotkey);
        Assert.Equal("Ctrl+Alt+F10", r.CaptureHotkey);
        Assert.Null(r.SaveFolder);
        Assert.False(r.AutoStart);
        Assert.False(r.CaptureSound);
    }

    [Theory]
    [InlineData("relative\\path")]
    [InlineData("")]
    [InlineData("C:\\bad|path")]
    public void InvalidSaveFolderResetToNull(string folder)
    {
        var json = JsonSerializer.Serialize(new { saveFolder = folder, captureHotkey = "Ctrl+K" });
        var r = LoadRaw(json);
        Assert.Null(r.SaveFolder);
        Assert.Equal("Ctrl+K", r.CaptureHotkey);
    }

    [Fact]
    public void MissingKeysGetDefaultsUnknownKeysIgnored()
    {
        var r = LoadRaw("{\"captureSound\": true, \"grayscaleOn\": true, \"whatever\": {\"a\":1}}");
        Assert.True(r.CaptureSound);
        Assert.Equal("Ctrl+Alt+F9", r.ToggleHotkey);
        Assert.True(r.AutoStart);
    }

    [Fact]
    public void ResolverDefaultsToPicturesLuminanceCapture()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "LuminanceCapture");
        Assert.Equal(expected, new AppSettings().ResolveSaveFolder());
        Assert.Equal(Path.Combine(@"X:\pics", "LuminanceCapture"), new AppSettings().ResolveSaveFolder(@"X:\pics"));
    }

    [Fact]
    public void ResolverUsesConfiguredFolder()
    {
        var s = new AppSettings { SaveFolder = @"D:\shots" };
        Assert.Equal(@"D:\shots", s.ResolveSaveFolder());
    }

    [Fact]
    public void SavedJsonHasExactlyTheFiveKeysAndNoOnOffState()
    {
        new SettingsStore(FilePath).Save(new AppSettings());
        using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "autoStart", "captureHotkey", "captureSound", "saveFolder", "toggleHotkey" }, keys);
    }
}
