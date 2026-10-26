using System.Runtime.InteropServices;

namespace LuminanceCapture.Core;

public static class WindowCaptureExclusion
{
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x11;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

    /// <summary>Hides the window from screen capture (incl. Windows.Graphics.Capture). Returns false on failure.</summary>
    public static bool Apply(IntPtr hwnd) => SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE);
}
