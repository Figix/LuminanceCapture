using System.IO;
namespace LuminanceCapture.Core;

public sealed record CapturePairNames(string ColorPath, string GrayPath)
{
    public string ColorFileName => Path.GetFileName(ColorPath);
    public string GrayFileName => Path.GetFileName(GrayPath);
}

public static class PairNameAllocator
{
    public static CapturePairNames Allocate(string folder, DateTime timestamp, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        string stem = timestamp.ToString("yyyy-MM-dd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        for (int n = 1; ; n++)
        {
            string s = n == 1 ? stem : $"{stem}_{n}";
            string color = Path.Combine(folder, s + "_color.png");
            string gray = Path.Combine(folder, s + "_gray.png");
            if (!exists(color) && !exists(gray))
                return new CapturePairNames(color, gray);
        }
    }
}
