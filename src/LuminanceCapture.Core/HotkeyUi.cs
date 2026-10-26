using System.Windows.Input;

namespace LuminanceCapture.Core;

public enum HotkeyCaptureKind { Ignore, NeedsModifier, Unsupported, Combo }

public readonly record struct HotkeyCaptureResult(HotkeyCaptureKind Kind, HotkeyCombo Combo);

/// <summary>Turns a key press in the settings textbox into a hotkey combo.</summary>
public static class HotkeyCapture
{
    public static HotkeyCaptureResult FromKey(Key key, Key systemKey, ModifierKeys modifiers)
    {
        var actual = key == Key.System ? systemKey : key;
        if (actual is Key.None or Key.System or Key.ImeProcessed or Key.DeadCharProcessed
            or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return new(HotkeyCaptureKind.Ignore, default);

        var mods = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Ctrl;
        if (modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;
        if (mods == HotkeyModifiers.None) return new(HotkeyCaptureKind.NeedsModifier, default);

        var combo = new HotkeyCombo(mods, actual);
        // Only keys that survive the text round trip can be saved and reloaded.
        if (!HotkeyParser.TryParse(combo.ToString(), out var parsed, out _) || parsed != combo)
            return new(HotkeyCaptureKind.Unsupported, default);
        return new(HotkeyCaptureKind.Combo, combo);
    }
}

public sealed record HotkeyApplyResult(bool Ok, string Message)
{
    public static HotkeyApplyResult Success { get; } = new(true, "");
}

public static class HotkeyMessages
{
    public static string Label(HotkeyId id) => id == HotkeyId.Toggle ? "흑백 토글" : "캡처";

    public static string ParseError(string english) => english switch
    {
        "empty" => "단축키가 비어 있습니다.",
        "needs a modifier" => "Ctrl, Alt, Shift, Win 중 하나 이상을 함께 눌러야 합니다.",
        _ => "사용할 수 없는 단축키입니다.",
    };

    public static string InUse(HotkeyId id, string text) =>
        $"{Label(id)} 단축키 {text}은(는) 다른 프로그램이 이미 사용 중이라 등록하지 못했습니다. 다른 조합을 골라 주세요.";
}

/// <summary>Validates and (re)registers hotkeys while keeping the previous working one on failure.</summary>
public sealed class HotkeyApplier
{
    private readonly Func<HotkeyId, string, (bool Ok, string Error)> _register;
    private readonly Action<HotkeyId> _unregister;

    public HotkeyApplier(Func<HotkeyId, string, (bool Ok, string Error)> register, Action<HotkeyId> unregister)
    {
        _register = register;
        _unregister = unregister;
    }

    /// <summary>Startup registration of one saved hotkey.</summary>
    public HotkeyApplyResult Register(HotkeyId id, string text)
    {
        if (!HotkeyParser.TryParse(text, out _, out var err))
            return new(false, $"{HotkeyMessages.Label(id)} 단축키: {HotkeyMessages.ParseError(err)}");
        var (ok, _) = _register(id, text);
        return ok ? HotkeyApplyResult.Success : new(false, HotkeyMessages.InUse(id, text));
    }

    /// <summary>Changes one hotkey. On any failure the previous working one (if any) is registered again.</summary>
    public HotkeyApplyResult Change(HotkeyId id, string newText, string otherText, string? previousWorking)
    {
        if (!HotkeyParser.TryParse(newText, out var combo, out var err))
            return new(false, $"{HotkeyMessages.Label(id)} 단축키: {HotkeyMessages.ParseError(err)}");
        if (HotkeyParser.TryParse(otherText, out var other, out _) && other == combo)
            return new(false, "흑백 토글과 캡처에 같은 조합을 쓸 수 없습니다.");

        _unregister(id);
        var (ok, _) = _register(id, newText);
        if (ok) return HotkeyApplyResult.Success;

        if (previousWorking is not null) _register(id, previousWorking);
        return new(false, HotkeyMessages.InUse(id, newText));
    }
}
