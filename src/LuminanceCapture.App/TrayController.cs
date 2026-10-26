using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace LuminanceCapture.App;

internal sealed class TrayController : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _toggleItem;
    private readonly Drawing.Icon _colorIcon;
    private readonly Drawing.Icon _grayIcon;

    public TrayController(Action openWindow, Action toggle, Action capture, Action openFolder, Action exit)
    {
        var size = WinForms.SystemInformation.SmallIconSize.Width;
        _colorIcon = IconArt.Create(gray: false, size);
        _grayIcon = IconArt.Create(gray: true, size);

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("창 열기", null, (_, _) => openWindow());
        _toggleItem = new WinForms.ToolStripMenuItem("흑백 켜기");
        _toggleItem.Click += (_, _) => toggle();
        menu.Items.Add(_toggleItem);
        menu.Items.Add("지금 캡처", null, (_, _) => capture());
        menu.Items.Add("저장 폴더 열기", null, (_, _) => openFolder());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => exit());

        _icon = new WinForms.NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => openWindow();
        Update(false);
    }

    public void Update(bool grayOn)
    {
        _icon.Icon = grayOn ? _grayIcon : _colorIcon;
        _icon.Text = grayOn ? "Luminance Capture - 흑백 켜짐" : "Luminance Capture - 흑백 꺼짐";
        _toggleItem.Text = grayOn ? "흑백 끄기" : "흑백 켜기";
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _icon.ContextMenuStrip?.Dispose();
        _colorIcon.Dispose();
        _grayIcon.Dispose();
    }
}
