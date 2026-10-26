using Microsoft.Win32;

namespace LuminanceCapture.Core;

/// <summary>Per-user login auto-start via a Run-key value. Key path and value name are injectable for tests.</summary>
public sealed class AutoStartManager
{
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "LuminanceCapture";
    public const string BackgroundArg = "--background";

    private readonly string _keyPath;
    private readonly string _valueName;

    public AutoStartManager(string keyPath = DefaultRunKeyPath, string valueName = DefaultValueName)
    {
        _keyPath = keyPath;
        _valueName = valueName;
    }

    public static string BuildCommand(string exePath) => $"\"{exePath}\" {BackgroundArg}";

    public void Register(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(_keyPath, writable: true);
        key.SetValue(_valueName, BuildCommand(exePath), RegistryValueKind.String);
    }

    public void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }

    public string? GetCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: false);
        return key?.GetValue(_valueName) as string;
    }

    public bool IsRegistered() => GetCommand() is not null;
}
