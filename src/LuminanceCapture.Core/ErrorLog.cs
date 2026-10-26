namespace LuminanceCapture.Core;

public static class ErrorLog
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LuminanceCapture", "log.txt");

    private static readonly object Gate = new();

    /// <summary>Appends one entry. Never throws.</summary>
    public static void Write(string message, Exception? ex = null, string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var text = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}" + (ex is null ? "" : Environment.NewLine + ex) + Environment.NewLine;
            lock (Gate) File.AppendAllText(path, text);
        }
        catch { }
    }
}
