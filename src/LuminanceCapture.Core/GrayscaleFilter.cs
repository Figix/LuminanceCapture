using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Win32;

namespace LuminanceCapture.Core;

public interface IColorEffectApi : IDisposable
{
    bool SetMatrix(float[] matrix5x5);
}

internal sealed class MagnificationApi : IColorEffectApi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MagColorEffect
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] Transform;
    }

    [DllImport("Magnification.dll")] private static extern bool MagInitialize();
    [DllImport("Magnification.dll")] private static extern bool MagUninitialize();
    [DllImport("Magnification.dll")] private static extern bool MagSetFullscreenColorEffect(ref MagColorEffect effect);

    private bool _initialized;

    public bool SetMatrix(float[] matrix5x5)
    {
        if (!_initialized)
        {
            _initialized = MagInitialize();
            if (!_initialized) return false;
        }
        var effect = new MagColorEffect { Transform = matrix5x5 };
        return MagSetFullscreenColorEffect(ref effect);
    }

    public void Dispose()
    {
        if (_initialized)
        {
            MagUninitialize();
            _initialized = false;
        }
    }
}

/// <summary>Live full-screen grayscale. Starts off; the effect dies with the process.</summary>
public sealed class GrayscaleFilter : IDisposable
{
    private static readonly float[] Identity =
    {
        1, 0, 0, 0, 0,
        0, 1, 0, 0, 0,
        0, 0, 1, 0, 0,
        0, 0, 0, 1, 0,
        0, 0, 0, 0, 1,
    };

    private readonly IColorEffectApi _api;
    private readonly float[] _grayMatrix = Luminance.CreateMagColorMatrix();
    private readonly object _gate = new();
    private readonly Dispatcher? _owner = Dispatcher.FromThread(Thread.CurrentThread);
    private bool _disposed;

    public GrayscaleFilter() : this(new MagnificationApi(), watchSystemEvents: true) { }

    public GrayscaleFilter(IColorEffectApi api, bool watchSystemEvents = false)
    {
        _api = api;
        if (watchSystemEvents)
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.DisplaySettingsChanged += OnSystemChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }
    }

    public bool IsOn { get; private set; }

    public event Action<bool>? Changed;

    public bool Toggle() => SetOn(!IsOn);

    /// <summary>Returns true when the requested state is now in effect.</summary>
    public bool SetOn(bool on)
    {
        lock (_gate)
        {
            if (_disposed) return false;
            if (!_api.SetMatrix(on ? _grayMatrix : Identity)) return false;
            if (IsOn == on) return true;
            IsOn = on;
        }
        Changed?.Invoke(on);
        return true;
    }

    /// <summary>Re-assert the effect after sleep, unlock or display changes.</summary>
    public void Reapply()
    {
        lock (_gate)
        {
            if (_disposed || !IsOn) return;
            _api.SetMatrix(_grayMatrix);
        }
    }

    // System events arrive on another thread; the magnification API is called from the creating (UI) thread.
    private void ReapplyOnOwner()
    {
        if (_owner is { } d && !d.CheckAccess()) d.BeginInvoke(Reapply);
        else Reapply();
    }

    private void OnPowerModeChanged(object? s, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) ReapplyOnOwner();
    }

    private void OnSystemChanged(object? s, EventArgs e) => ReapplyOnOwner();

    private void OnSessionSwitch(object? s, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
            ReapplyOnOwner();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.DisplaySettingsChanged -= OnSystemChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _api.SetMatrix(Identity);
            _api.Dispose();
            IsOn = false;
        }
    }
}
