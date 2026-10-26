using System.Runtime.InteropServices;

namespace LuminanceCapture.Core;

public static class MonitorLocator
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    /// <summary>Monitor under the pointer right now, using physical coordinates regardless of process DPI mode.</summary>
    public static CaptureTarget FromCursor()
    {
        var previous = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            if (!GetCursorPos(out var pt))
                throw new InvalidOperationException("GetCursorPos failed.");
            var hmon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
            if (hmon == IntPtr.Zero)
                throw new InvalidOperationException("No monitor found for the pointer position.");
            return new CaptureTarget(hmon);
        }
        finally
        {
            if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous);
        }
    }
}
