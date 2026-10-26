using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using LuminanceCapture.Core;

namespace LuminanceCapture.App;

public partial class MainWindow : Window
{
    private readonly AppHost _host;
    private bool _loading = true;

    internal MainWindow(AppHost host)
    {
        _host = host;
        InitializeComponent();

        SourceInitialized += (_, _) =>
        {
            // The settings window must not end up in saved captures either.
            if (!WindowCaptureExclusion.Apply(new WindowInteropHelper(this).Handle))
                ErrorLog.Write("WindowCaptureExclusion.Apply failed for main window");
        };

        ToggleBox.Text = host.WorkingOrSaved(HotkeyId.Toggle);
        CaptureBox.Text = host.WorkingOrSaved(HotkeyId.Capture);
        FolderBox.Text = host.Settings.ResolveSaveFolder();
        AutoStartBox.IsChecked = host.Settings.AutoStart;
        SoundBox.IsChecked = host.Settings.CaptureSound;
        RefreshStatus(host.Filter.IsOn);
        if (host.StartupProblems.Count > 0) ShowMessage(string.Join("\n", host.StartupProblems));
        _loading = false;
    }

    public void RefreshStatus(bool grayOn)
    {
        StatusText.Text = grayOn ? "흑백: 켜짐" : "흑백: 꺼짐";
        ToggleButton.Content = grayOn ? "흑백 끄기" : "흑백 켜기";
    }

    public void ShowMessage(string text)
    {
        MessageText.Text = text;
        MessageText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Shows the window (even if hidden or minimized) and brings it to the front.</summary>
    public void BringToFront()
    {
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero) Native.SetForegroundWindow(hwnd);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_host.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    private void OnToggleClick(object sender, RoutedEventArgs e) => _host.ToggleGray();

    private void OnCaptureClick(object sender, RoutedEventArgs e) => _host.CaptureNow();

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && (Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift)) return;
        e.Handled = true;

        var box = (TextBox)sender;
        var id = (string)box.Tag == "Toggle" ? HotkeyId.Toggle : HotkeyId.Capture;
        var r = HotkeyCapture.FromKey(e.Key, e.SystemKey, Keyboard.Modifiers);
        switch (r.Kind)
        {
            case HotkeyCaptureKind.Ignore:
                return;
            case HotkeyCaptureKind.NeedsModifier:
                ShowMessage("Ctrl, Alt, Shift, Win 중 하나 이상을 함께 눌러야 합니다.");
                return;
            case HotkeyCaptureKind.Unsupported:
                ShowMessage("이 키는 단축키로 쓸 수 없습니다. 다른 키를 눌러 주세요.");
                return;
        }

        var text = r.Combo.ToString();
        box.Text = text;
        var result = _host.ChangeHotkey(id, text);
        ShowMessage(result.Ok ? "" : result.Message);
    }

    private void OnHotkeyLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var box = (TextBox)sender;
        var id = (string)box.Tag == "Toggle" ? HotkeyId.Toggle : HotkeyId.Capture;
        box.Text = _host.WorkingOrSaved(id);
    }

    private void OnPickFolderClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "캡처를 저장할 폴더 선택",
            InitialDirectory = _host.Settings.ResolveSaveFolder(),
        };
        if (dlg.ShowDialog(this) != true) return;
        var error = _host.SetSaveFolder(dlg.FolderName);
        FolderBox.Text = _host.Settings.ResolveSaveFolder();
        if (error is not null) ShowMessage(error);
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e) => _host.OpenSaveFolder();

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var err = ReferenceEquals(sender, AutoStartBox)
            ? _host.SetAutoStart(AutoStartBox.IsChecked == true)
            : _host.SetCaptureSound(SoundBox.IsChecked == true);
        if (err is not null) ShowMessage(err);
    }
}
