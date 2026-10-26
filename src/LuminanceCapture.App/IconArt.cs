using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace LuminanceCapture.App;

/// <summary>Tray icons drawn in code: color bands when the screen is in color, the matching grays when it is gray.</summary>
internal static class IconArt
{
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);

    private static readonly Color[] Colors =
    {
        Color.FromArgb(230, 57, 53), Color.FromArgb(67, 160, 71), Color.FromArgb(30, 100, 220),
    };
    private static readonly Color[] Grays =
    {
        Color.FromArgb(64, 64, 64), Color.FromArgb(150, 150, 150), Color.FromArgb(40, 40, 40),
    };

    public static Icon Create(bool gray, int size)
    {
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var pad = Math.Max(1, (int)(size * 0.06));
            var d = size - 2 * pad;
            using var clip = new GraphicsPath();
            clip.AddEllipse(pad, pad, d, d);
            g.SetClip(clip);
            var palette = gray ? Grays : Colors;
            var bh = d / 3f;
            for (var i = 0; i < 3; i++)
            {
                using var b = new SolidBrush(palette[i]);
                g.FillRectangle(b, pad, pad + i * bh, d, bh + 1);
            }
            g.ResetClip();
            using var pen = new Pen(Color.FromArgb(gray ? 200 : 70, gray ? 200 : 70, gray ? 200 : 70), Math.Max(1f, size / 16f));
            g.DrawEllipse(pen, pad, pad, d, d);
        }
        var h = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(h);
            return (Icon)tmp.Clone();
        }
        finally { DestroyIcon(h); }
    }
}
