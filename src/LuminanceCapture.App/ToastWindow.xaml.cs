using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LuminanceCapture.Core;
using WinForms = System.Windows.Forms;

namespace LuminanceCapture.App;

/// <summary>Borderless, topmost, non-activating toast excluded from screen capture. One instance; a new toast replaces the old.</summary>
public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private readonly IntPtr _hwnd;

    public ToastWindow()
    {
        InitializeComponent();
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        Native.AddExStyle(_hwnd, Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT);
        if (!WindowCaptureExclusion.Apply(_hwnd))
            ErrorLog.Write("WindowCaptureExclusion.Apply failed for toast window");
        _timer.Tick += (_, _) => { _timer.Stop(); Hide(); };
    }

    public void ShowToast(string text, bool error, TimeSpan duration)
    {
        Message.Text = text;
        Root.Background = new SolidColorBrush(error ? Color.FromRgb(0x8B, 0x1E, 0x1E) : Color.FromRgb(0x2B, 0x2B, 0x2B));
        Root.BorderBrush = new SolidColorBrush(error ? Color.FromRgb(0xE5, 0x73, 0x73) : Color.FromRgb(0x55, 0x55, 0x55));

        var pos = WinForms.Cursor.Position;
        var screen = WinForms.Screen.FromPoint(pos);
        var mon = Native.MonitorFromPoint(new Native.POINT { X = pos.X, Y = pos.Y }, Native.MONITOR_DEFAULTTONEAREST);
        uint dpi = 96;
        try { if (Native.GetDpiForMonitor(mon, 0, out var dx, out _) == 0 && dx > 0) dpi = dx; } catch { }
        var scale = dpi / 96.0;
        var w = (int)Math.Round(360 * scale);
        var h = (int)Math.Round((error ? 88 : 64) * scale);
        var margin = (int)Math.Round(24 * scale);
        var area = screen.WorkingArea;

        if (!IsVisible) Show();
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, area.Right - w - margin, area.Bottom - h - margin, w, h,
            Native.SWP_NOACTIVATE | Native.SWP_SHOWWINDOW);

        _timer.Stop();
        _timer.Interval = duration;
        _timer.Start();
    }
}
