using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using LuminanceCapture.Core;

namespace LuminanceCapture.App;

/// <summary>The single place that composes the services, tray, toast and settings window.</summary>
internal sealed class AppHost : IDisposable
{
    public const string SettingsEnv = "LUMINANCECAPTURE_SETTINGS";
    public const string NoAutoStartEnv = "LUMINANCECAPTURE_NO_AUTOSTART";

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly SingleInstance _single;
    private readonly SettingsStore _store;
    private readonly AutoStartManager _autoStart = new();
    private readonly bool _noAutoStart;
    private readonly List<string> _startupProblems = new();
    private readonly Dictionary<HotkeyId, string?> _working = new()
    {
        [HotkeyId.Toggle] = null,
        [HotkeyId.Capture] = null,
    };
    private HotkeyService? _hotkeys;
    private HotkeyApplier? _applier;
    private CaptureService? _capture;
    private TrayController? _tray;
    private ToastWindow? _toast;
    private bool _disposed;

    public AppHost(SingleInstance single)
    {
        _single = single;
        var path = Environment.GetEnvironmentVariable(SettingsEnv);
        _store = new SettingsStore(string.IsNullOrWhiteSpace(path) ? null : path);
        _noAutoStart = Environment.GetEnvironmentVariable(NoAutoStartEnv) == "1";
        Settings = new AppSettings();
        Filter = new GrayscaleFilter();
        Window = null!;
    }

    public AppSettings Settings { get; private set; }
    public GrayscaleFilter Filter { get; }
    public MainWindow Window { get; private set; }
    public bool IsExiting { get; private set; }
    public IReadOnlyList<string> StartupProblems => _startupProblems;

    public string WorkingOrSaved(HotkeyId id) =>
        _working[id] ?? (id == HotkeyId.Toggle ? Settings.ToggleHotkey : Settings.CaptureHotkey);

    public void Start(bool background)
    {
        var firstRun = !File.Exists(_store.FilePath);
        Settings = _store.Load();
        if (firstRun) SaveSettings();
        SyncAutoStart();

        Filter.Changed += on => _dispatcher.BeginInvoke(() => OnFilterChanged(on));

        _hotkeys = new HotkeyService();
        _applier = new HotkeyApplier(
            (id, text) => (_hotkeys.TryRegister(id, text, out var err), err),
            id => _hotkeys.Unregister(id));
        _hotkeys.Pressed += OnHotkeyPressed;
        foreach (var (id, text) in new[] { (HotkeyId.Toggle, Settings.ToggleHotkey), (HotkeyId.Capture, Settings.CaptureHotkey) })
        {
            var r = _applier.Register(id, text);
            if (r.Ok) _working[id] = text;
            else _startupProblems.Add(r.Message);
        }

        _capture = new CaptureService(new WgcCaptureSource(), () => Settings.ResolveSaveFolder());
        _capture.CaptureCompleted += r => _dispatcher.BeginInvoke(() => OnCaptureCompleted(r));

        _toast = new ToastWindow();
        _tray = new TrayController(ShowWindow, ToggleGray, CaptureNow, OpenSaveFolder, Exit);
        Window = new MainWindow(this);

        _single.StartListening(() => _dispatcher.BeginInvoke(ShowWindow));

        if (!background || _startupProblems.Count > 0) ShowWindow();
    }

    // ---- actions ----

    public void ShowWindow() => Window.BringToFront();

    public void ToggleGray()
    {
        if (!Filter.Toggle()) ShowToast("흑백 전환에 실패했습니다.", error: true);
    }

    public void CaptureNow() => _capture?.Request();

    public void OpenSaveFolder()
    {
        try
        {
            var folder = Settings.ResolveSaveFolder();
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorLog.Write("OpenSaveFolder failed", ex);
            ShowToast("저장 폴더를 열지 못했습니다.", error: true);
        }
    }

    public void Exit()
    {
        if (IsExiting) return;
        IsExiting = true;
        Dispose();
        Application.Current.Shutdown();
    }

    // ---- settings ----

    public HotkeyApplyResult ChangeHotkey(HotkeyId id, string text)
    {
        var other = id == HotkeyId.Toggle ? HotkeyId.Capture : HotkeyId.Toggle;
        var r = _applier!.Change(id, text, WorkingOrSaved(other), _working[id]);
        if (!r.Ok) return r;
        _working[id] = text;
        if (id == HotkeyId.Toggle) Settings.ToggleHotkey = text; else Settings.CaptureHotkey = text;
        var err = SaveSettings();
        return err is null ? r : new HotkeyApplyResult(true, err);
    }

    public string? SetSaveFolder(string folder)
    {
        Settings.SaveFolder = folder;
        return SaveSettings();
    }

    public string? SetCaptureSound(bool on)
    {
        Settings.CaptureSound = on;
        return SaveSettings();
    }

    public string? SetAutoStart(bool on)
    {
        Settings.AutoStart = on;
        string? regError = null;
        try { SyncAutoStart(throwOnError: true); }
        catch (Exception ex)
        {
            ErrorLog.Write("autostart change failed", ex);
            regError = "자동 실행 설정을 바꾸지 못했습니다.";
        }
        return SaveSettings() ?? regError;
    }

    private void SyncAutoStart(bool throwOnError = false)
    {
        if (_noAutoStart) return;
        try
        {
            var exe = Environment.ProcessPath;
            if (exe is null) return;
            if (Settings.AutoStart)
            {
                if (_autoStart.GetCommand() != AutoStartManager.BuildCommand(exe)) _autoStart.Register(exe);
            }
            else _autoStart.Unregister();
        }
        catch (Exception ex)
        {
            ErrorLog.Write("SyncAutoStart failed", ex);
            if (throwOnError) throw;
        }
    }

    private string? SaveSettings()
    {
        try { _store.Save(Settings); return null; }
        catch (Exception ex)
        {
            ErrorLog.Write("settings save failed", ex);
            return "설정을 저장하지 못했습니다.";
        }
    }

    // ---- event handlers (UI thread) ----

    private void OnHotkeyPressed(HotkeyId id)
    {
        if (id == HotkeyId.Toggle) ToggleGray(); else CaptureNow();
    }

    private void OnFilterChanged(bool on)
    {
        if (_disposed) return;
        _tray?.Update(on);
        Window?.RefreshStatus(on);
    }

    private void OnCaptureCompleted(CaptureResult r)
    {
        if (_disposed) return;
        if (r.Success)
        {
            var stem = Path.GetFileNameWithoutExtension(r.ColorPath) ?? "";
            if (stem.EndsWith("_color", StringComparison.Ordinal)) stem = stem[..^"_color".Length];
            if (Settings.CaptureSound) System.Media.SystemSounds.Asterisk.Play();
            ShowToast("저장됨: " + stem, error: false);
        }
        else
        {
            ShowToast("캡처에 실패했습니다: " + r.Error, error: true);
        }
    }

    private void ShowToast(string text, bool error)
    {
        try { _toast?.ShowToast(text, error, TimeSpan.FromMilliseconds(error ? 3500 : 1500)); }
        catch (Exception ex) { ErrorLog.Write("toast failed", ex); }
    }

    /// <summary>Safe from any thread and any exit path: screen goes back to color.</summary>
    public void EmergencyRestore()
    {
        try { Filter.Dispose(); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        EmergencyRestore(); // color first, everything else after
        try { _hotkeys?.Dispose(); } catch (Exception ex) { ErrorLog.Write("hotkeys dispose", ex); }
        try { _tray?.Dispose(); } catch (Exception ex) { ErrorLog.Write("tray dispose", ex); }
        try { _capture?.Dispose(); } catch (Exception ex) { ErrorLog.Write("capture dispose", ex); }
        try { _toast?.Close(); } catch { }
        try { _single.Dispose(); } catch { }
    }
}
