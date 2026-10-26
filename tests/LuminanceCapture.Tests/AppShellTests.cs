using System.Windows.Input;
using LuminanceCapture.Core;
using Microsoft.Win32;

namespace LuminanceCapture.Tests;

public class AutoStartManagerTests : IDisposable
{
    private readonly string _keyPath = @"Software\LuminanceCaptureTests\" + Guid.NewGuid().ToString("N");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

    [Fact]
    public void RegisterWritesQuotedPathWithBackgroundArg()
    {
        var m = new AutoStartManager(_keyPath);
        Assert.False(m.IsRegistered());
        m.Register(@"C:\Program Files\Lum Cap\LuminanceCapture.exe");
        Assert.True(m.IsRegistered());
        Assert.Equal("\"C:\\Program Files\\Lum Cap\\LuminanceCapture.exe\" --background", m.GetCommand());
    }

    [Fact]
    public void UnregisterRemovesValueAndIsIdempotent()
    {
        var m = new AutoStartManager(_keyPath);
        m.Register(@"C:\a.exe");
        m.Unregister();
        Assert.False(m.IsRegistered());
        m.Unregister();
        new AutoStartManager(@"Software\LuminanceCaptureTests\missing-" + Guid.NewGuid().ToString("N")).Unregister();
    }

    [Fact]
    public void RegisterOverwritesPreviousPath()
    {
        var m = new AutoStartManager(_keyPath);
        m.Register(@"C:\old.exe");
        m.Register(@"C:\new.exe");
        Assert.Equal("\"C:\\new.exe\" --background", m.GetCommand());
    }

    [Fact]
    public void DefaultsMatchFixedSpec()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", AutoStartManager.DefaultRunKeyPath);
        Assert.Equal("LuminanceCapture", AutoStartManager.DefaultValueName);
    }
}

public class SingleInstanceTests
{
    [Fact]
    public void SecondAcquireFailsAndSignalReachesFirst()
    {
        var name = "LumTest." + Guid.NewGuid().ToString("N");
        using var first = new SingleInstance(name);
        Assert.True(first.TryAcquire());
        using var got = new ManualResetEventSlim();
        first.StartListening(got.Set);

        using var second = new SingleInstance(name);
        Assert.False(second.TryAcquire());
        second.SignalExisting();
        Assert.True(got.Wait(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void AcquireSucceedsAgainAfterFirstDisposed()
    {
        var name = "LumTest." + Guid.NewGuid().ToString("N");
        var first = new SingleInstance(name);
        Assert.True(first.TryAcquire());
        first.Dispose();
        using var again = new SingleInstance(name);
        Assert.True(again.TryAcquire());
    }

    [Fact]
    public void NamesAreSessionLocalAndDistinct()
    {
        var s = new SingleInstance("X");
        Assert.StartsWith(@"Local\", s.MutexName);
        Assert.NotEqual(s.MutexName, s.EventName);
    }
}

public class HotkeyCaptureTests
{
    [Fact]
    public void CtrlAltF9GivesText()
    {
        var r = HotkeyCapture.FromKey(Key.F9, Key.None, ModifierKeys.Control | ModifierKeys.Alt);
        Assert.Equal(HotkeyCaptureKind.Combo, r.Kind);
        Assert.Equal("Ctrl+Alt+F9", r.Combo.ToString());
    }

    [Fact]
    public void AltComboArrivesAsSystemKey()
    {
        var r = HotkeyCapture.FromKey(Key.System, Key.F10, ModifierKeys.Alt | ModifierKeys.Shift);
        Assert.Equal("Alt+Shift+F10", r.Combo.ToString());
        Assert.Equal(HotkeyCaptureKind.Combo, r.Kind);
    }

    [Fact]
    public void ModifierOnlyIsIgnored()
    {
        Assert.Equal(HotkeyCaptureKind.Ignore, HotkeyCapture.FromKey(Key.LeftCtrl, Key.None, ModifierKeys.Control).Kind);
        Assert.Equal(HotkeyCaptureKind.Ignore, HotkeyCapture.FromKey(Key.LWin, Key.None, ModifierKeys.Windows).Kind);
        Assert.Equal(HotkeyCaptureKind.Ignore, HotkeyCapture.FromKey(Key.System, Key.LeftAlt, ModifierKeys.Alt).Kind);
    }

    [Fact]
    public void KeyWithoutModifierNeedsModifier()
    {
        Assert.Equal(HotkeyCaptureKind.NeedsModifier, HotkeyCapture.FromKey(Key.F9, Key.None, ModifierKeys.None).Kind);
    }

    [Fact]
    public void DigitAndLetterRoundTrip()
    {
        var d = HotkeyCapture.FromKey(Key.D5, Key.None, ModifierKeys.Control);
        Assert.Equal("Ctrl+5", d.Combo.ToString());
        var l = HotkeyCapture.FromKey(Key.G, Key.None, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Windows);
        Assert.Equal("Ctrl+Shift+Win+G", l.Combo.ToString());
        Assert.True(HotkeyParser.TryParse(l.Combo.ToString(), out var back, out _));
        Assert.Equal(l.Combo, back);
    }
}

public class HotkeyApplierTests
{
    private sealed class Fake
    {
        public readonly Dictionary<HotkeyId, string> Registered = new();
        public readonly HashSet<string> Taken = new();
        public readonly HotkeyApplier Applier;

        public Fake()
        {
            Applier = new HotkeyApplier(
                (id, text) =>
                {
                    if (Taken.Contains(text)) return (false, "taken");
                    Registered[id] = text;
                    return (true, "");
                },
                id => Registered.Remove(id));
        }
    }

    [Fact]
    public void ChangeSucceedsAndReplacesRegistration()
    {
        var f = new Fake();
        f.Registered[HotkeyId.Toggle] = "Ctrl+Alt+F9";
        var r = f.Applier.Change(HotkeyId.Toggle, "Ctrl+Alt+F8", "Ctrl+Alt+F10", "Ctrl+Alt+F9");
        Assert.True(r.Ok);
        Assert.Equal("Ctrl+Alt+F8", f.Registered[HotkeyId.Toggle]);
    }

    [Fact]
    public void TakenComboKeepsPreviousAndNamesTheHotkey()
    {
        var f = new Fake();
        f.Registered[HotkeyId.Capture] = "Ctrl+Alt+F10";
        f.Taken.Add("Ctrl+Alt+F7");
        var r = f.Applier.Change(HotkeyId.Capture, "Ctrl+Alt+F7", "Ctrl+Alt+F9", "Ctrl+Alt+F10");
        Assert.False(r.Ok);
        Assert.Contains("캡처", r.Message);
        Assert.Contains("Ctrl+Alt+F7", r.Message);
        Assert.Equal("Ctrl+Alt+F10", f.Registered[HotkeyId.Capture]);
    }

    [Fact]
    public void SameComboAsOtherIsRejectedWithoutTouchingRegistration()
    {
        var f = new Fake();
        f.Registered[HotkeyId.Toggle] = "Ctrl+Alt+F9";
        var r = f.Applier.Change(HotkeyId.Toggle, "ctrl+alt+f10", "Ctrl+Alt+F10", "Ctrl+Alt+F9");
        Assert.False(r.Ok);
        Assert.Equal("Ctrl+Alt+F9", f.Registered[HotkeyId.Toggle]);
    }

    [Fact]
    public void NoModifierIsRejected()
    {
        var f = new Fake();
        f.Registered[HotkeyId.Toggle] = "Ctrl+Alt+F9";
        var r = f.Applier.Change(HotkeyId.Toggle, "F8", "Ctrl+Alt+F10", "Ctrl+Alt+F9");
        Assert.False(r.Ok);
        Assert.Equal("Ctrl+Alt+F9", f.Registered[HotkeyId.Toggle]);
    }

    [Fact]
    public void StartupRegisterReportsWhichFailed()
    {
        var f = new Fake();
        f.Taken.Add("Ctrl+Alt+F10");
        Assert.True(f.Applier.Register(HotkeyId.Toggle, "Ctrl+Alt+F9").Ok);
        var r = f.Applier.Register(HotkeyId.Capture, "Ctrl+Alt+F10");
        Assert.False(r.Ok);
        Assert.Contains("캡처", r.Message);
        Assert.False(f.Registered.ContainsKey(HotkeyId.Capture));
    }
}

public class ErrorLogTests
{
    [Fact]
    public void AppendsEntriesAndCreatesDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumlog-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "log.txt");
        try
        {
            ErrorLog.Write("one", new InvalidOperationException("boom"), path);
            ErrorLog.Write("two", null, path);
            var text = File.ReadAllText(path);
            Assert.Contains("one", text);
            Assert.Contains("boom", text);
            Assert.Contains("two", text);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void NeverThrowsOnBadPath()
    {
        ErrorLog.Write("x", null, "Z:\\definitely\\no\\such<>drive\\log.txt");
    }
}
