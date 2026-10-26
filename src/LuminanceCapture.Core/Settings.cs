using System.IO;
using System.Text.Json;

namespace LuminanceCapture.Core;

// Grayscale on/off state is intentionally not part of settings and is never persisted.
public sealed class AppSettings
{
    public const string DefaultToggleHotkey = "Ctrl+Alt+F9";
    public const string DefaultCaptureHotkey = "Ctrl+Alt+F10";

    public string ToggleHotkey { get; set; } = DefaultToggleHotkey;
    public string CaptureHotkey { get; set; } = DefaultCaptureHotkey;
    public string? SaveFolder { get; set; }
    public bool AutoStart { get; set; } = true;
    public bool CaptureSound { get; set; }

    public string ResolveSaveFolder(string? picturesFolder = null) =>
        SaveFolder ?? Path.Combine(
            picturesFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "LuminanceCapture");
}

public sealed class SettingsStore
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LuminanceCapture", "settings.json");

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public string FilePath { get; }

    public SettingsStore(string? path = null) => FilePath = path ?? DefaultPath;

    public AppSettings Load()
    {
        var result = new AppSettings();
        try
        {
            if (!File.Exists(FilePath)) return result;
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            var root = doc.RootElement;

            if (HotkeyOrNull(root, "toggleHotkey") is { } t) result.ToggleHotkey = t;
            if (HotkeyOrNull(root, "captureHotkey") is { } c) result.CaptureHotkey = c;
            if (BoolOrNull(root, "autoStart") is { } a) result.AutoStart = a;
            if (BoolOrNull(root, "captureSound") is { } s) result.CaptureSound = s;
            result.SaveFolder = FolderOrNull(root, "saveFolder");
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new AppSettings();
        }
        return result;
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["toggleHotkey"] = settings.ToggleHotkey,
            ["captureHotkey"] = settings.CaptureHotkey,
            ["saveFolder"] = settings.SaveFolder,
            ["autoStart"] = settings.AutoStart,
            ["captureSound"] = settings.CaptureSound,
        }, WriteOptions);

        var tmp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(tmp, json);
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    private static string? HotkeyOrNull(JsonElement root, string key) =>
        root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(p.GetString()) ? p.GetString() : null;

    private static bool? BoolOrNull(JsonElement root, string key) =>
        root.TryGetProperty(key, out var p) && p.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? p.GetBoolean() : null;

    private static string? FolderOrNull(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var p) || p.ValueKind != JsonValueKind.String) return null;
        var s = p.GetString();
        if (string.IsNullOrWhiteSpace(s)) return null;
        try
        {
            return Path.IsPathFullyQualified(s)
                   && s.IndexOfAny(Path.GetInvalidPathChars()) < 0
                   && Path.GetFullPath(s) is not null ? s : null;
        }
        catch (ArgumentException) { return null; }
        catch (NotSupportedException) { return null; }
    }
}
