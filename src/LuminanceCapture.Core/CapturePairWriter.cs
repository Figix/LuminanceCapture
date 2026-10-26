using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LuminanceCapture.Core;

public static class CapturePairWriter
{
    /// <summary>
    /// Writes color + gray PNGs atomically (both or neither, never overwriting).
    /// The gray image is always derived in software from the color bytes.
    /// </summary>
    public static CapturePairNames Write(string folder, DateTime timestamp, CapturedFrame frame)
    {
        Directory.CreateDirectory(folder);

        var gray = new byte[frame.Bgra.Length];
        Luminance.ToGrayscale(frame.Bgra, gray);
        byte[] colorPng = EncodePng(frame.Bgra, frame.Width, frame.Height);
        byte[] grayPng = EncodePng(gray, frame.Width, frame.Height);

        var names = PairNameAllocator.Allocate(folder, timestamp);
        string suffix = "." + Guid.NewGuid().ToString("N") + ".tmp";
        string colorTmp = names.ColorPath + suffix;
        string grayTmp = names.GrayPath + suffix;
        bool colorMoved = false;
        try
        {
            File.WriteAllBytes(colorTmp, colorPng);
            File.WriteAllBytes(grayTmp, grayPng);
            File.Move(colorTmp, names.ColorPath);
            colorMoved = true;
            File.Move(grayTmp, names.GrayPath);
            return names;
        }
        catch
        {
            TryDelete(colorTmp);
            TryDelete(grayTmp);
            if (colorMoved) TryDelete(names.ColorPath);
            throw;
        }
    }

    public static byte[] EncodePng(byte[] bgra, int width, int height)
    {
        // Bgr32 ignores the alpha byte, so the PNG is written as opaque RGB.
        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, bgra, width * 4);
        bmp.Freeze();
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
