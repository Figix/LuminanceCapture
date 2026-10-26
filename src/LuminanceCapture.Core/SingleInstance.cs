namespace LuminanceCapture.Core;

/// <summary>Named-mutex single instance plus a named event the second launch uses to wake the first.</summary>
public sealed class SingleInstance : IDisposable
{
    public const string DefaultName = "LuminanceCapture.SingleInstance";

    private readonly string _mutexName;
    private readonly string _eventName;
    private Mutex? _mutex;
    private EventWaitHandle? _event;
    private readonly ManualResetEvent _stop = new(false);
    private Thread? _listener;

    public SingleInstance(string name = DefaultName)
    {
        _mutexName = @"Local\" + name + ".Mutex";
        _eventName = @"Local\" + name + ".Show";
    }

    public string MutexName => _mutexName;
    public string EventName => _eventName;

    /// <summary>True when this process is the first instance.</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(false, _mutexName, out var createdNew);
        if (!createdNew)
        {
            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        _event = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName);
        return true;
    }

    /// <summary>Asks the first instance to show its window.</summary>
    public void SignalExisting()
    {
        using var ev = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName);
        ev.Set();
    }

    /// <summary>Runs onSignal (on a background thread) each time a later launch signals us.</summary>
    public void StartListening(Action onSignal)
    {
        if (_event is null) throw new InvalidOperationException("Not the owning instance.");
        var ev = _event;
        _listener = new Thread(() =>
        {
            var handles = new WaitHandle[] { ev, _stop };
            while (WaitHandle.WaitAny(handles) == 0)
            {
                try { onSignal(); } catch { }
            }
        }) { IsBackground = true, Name = "SingleInstanceListener" };
        _listener.Start();
    }

    public void Dispose()
    {
        _stop.Set();
        _listener?.Join(1000);
        _event?.Dispose();
        _event = null;
        _mutex?.Dispose();
        _mutex = null;
    }
}
