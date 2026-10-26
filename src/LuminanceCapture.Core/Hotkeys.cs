using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace LuminanceCapture.Core;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Ctrl = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

public readonly record struct HotkeyCombo(HotkeyModifiers Modifiers, Key Key)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        var k = Key is >= Key.D0 and <= Key.D9 ? ((int)Key - (int)Key.D0).ToString() : Key.ToString();
        parts.Add(k);
        return string.Join("+", parts);
    }
}

public static class HotkeyParser
{
    /// <summary>Parses "Ctrl+Alt+F9"-style text. Requires at least one modifier and exactly one non-modifier key.</summary>
    public static bool TryParse(string? text, out HotkeyCombo combo, out string error)
    {
        combo = default;
        error = "";
        if (string.IsNullOrWhiteSpace(text)) { error = "empty"; return false; }

        var mods = HotkeyModifiers.None;
        Key? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
        {
            if (raw.Length == 0) { error = "malformed"; return false; }
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": mods |= HotkeyModifiers.Ctrl; continue;
                case "alt": mods |= HotkeyModifiers.Alt; continue;
                case "shift": mods |= HotkeyModifiers.Shift; continue;
                case "win": case "windows": mods |= HotkeyModifiers.Win; continue;
            }
            if (key != null) { error = "more than one key"; return false; }
            key = ParseKey(raw);
            if (key == null) { error = $"unknown key '{raw}'"; return false; }
        }
        if (key == null) { error = "no key"; return false; }
        if (mods == HotkeyModifiers.None) { error = "needs a modifier"; return false; }
        combo = new HotkeyCombo(mods, key.Value);
        return true;
    }

    /// <summary>Validates the app's pair of hotkeys: each parses, and they are not the same combination.</summary>
    public static bool ValidatePair(string? toggle, string? capture, out string error)
    {
        if (!TryParse(toggle, out var a, out var e1)) { error = "toggle: " + e1; return false; }
        if (!TryParse(capture, out var b, out var e2)) { error = "capture: " + e2; return false; }
        if (a == b) { error = "same combination"; return false; }
        error = "";
        return true;
    }

    private static Key? ParseKey(string raw)
    {
        if (raw.Length == 1 && char.IsDigit(raw[0])) return Key.D0 + (raw[0] - '0');
        if (Enum.TryParse<Key>(raw, ignoreCase: true, out var k) && k != Key.None && !IsModifierKey(k) && Enum.IsDefined(k))
            return k;
        return null;
    }

    private static bool IsModifierKey(Key k) =>
        k is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System;
}

public enum HotkeyId { Toggle = 1, Capture = 2 }

/// <summary>Global hotkeys on a message-only window. Create on the UI (STA) thread.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static readonly IntPtr HwndMessage = new(-3);

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly HashSet<HotkeyId> _registered = new();

    public HotkeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("LuminanceCaptureHotkeys") { ParentWindow = HwndMessage, Width = 0, Height = 0 });
        _source.AddHook(WndProc);
    }

    public event Action<HotkeyId>? Pressed;

    /// <summary>(Re)registers one hotkey. Returns false with a reason when invalid or already taken by another program.</summary>
    public bool TryRegister(HotkeyId id, string text, out string error)
    {
        if (!HotkeyParser.TryParse(text, out var combo, out error)) return false;
        Unregister(id);
        var vk = (uint)KeyInterop.VirtualKeyFromKey(combo.Key);
        if (!RegisterHotKey(_source.Handle, (int)id, (uint)combo.Modifiers | ModNoRepeat, vk))
        {
            error = $"'{combo}' is already used by another program";
            return false;
        }
        _registered.Add(id);
        return true;
    }

    public void Unregister(HotkeyId id)
    {
        if (_registered.Remove(id)) UnregisterHotKey(_source.Handle, (int)id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && Enum.IsDefined(typeof(HotkeyId), wParam.ToInt32()))
        {
            handled = true;
            Pressed?.Invoke((HotkeyId)wParam.ToInt32());
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _registered.ToArray()) Unregister(id);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
