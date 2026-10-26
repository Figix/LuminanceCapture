using System.Collections.Concurrent;

namespace LuminanceCapture.Core;

/// <summary>Serial capture queue: every request yields one pair (or one failure), strictly in order.</summary>
public sealed class CaptureService : IDisposable
{
    private sealed record Job(CaptureTarget Target, DateTime Timestamp, string Folder);

    private readonly ICaptureSource _source;
    private readonly Func<string> _folderProvider;
    private readonly Func<CaptureTarget> _targetProvider;
    private readonly BlockingCollection<Job> _queue = new();
    private readonly Thread _worker;
    private bool _disposed;

    public event Action<CaptureResult>? CaptureCompleted;

    public CaptureService(ICaptureSource source, Func<string> folderProvider, Func<CaptureTarget>? targetProvider = null)
    {
        _source = source;
        _folderProvider = folderProvider;
        _targetProvider = targetProvider ?? MonitorLocator.FromCursor;
        _worker = new Thread(Run) { IsBackground = true, Name = "CaptureService" };
        _worker.Start();
    }

    /// <summary>Non-blocking. Picks the monitor and folder now, captures later on the worker.</summary>
    public void Request()
    {
        var timestamp = DateTime.Now;
        Job job;
        try
        {
            job = new Job(_targetProvider(), timestamp, _folderProvider());
        }
        catch (Exception e)
        {
            RaiseCompleted(new CaptureResult(false, null, null, "Capture failed: " + e.Message));
            return;
        }

        try { _queue.Add(job); }
        catch (Exception e) when (e is InvalidOperationException or ObjectDisposedException)
        {
            RaiseCompleted(new CaptureResult(false, null, null, "Capture service is stopped."));
        }
    }

    private void Run()
    {
        foreach (var job in _queue.GetConsumingEnumerable())
            RaiseCompleted(Process(job));
    }

    private CaptureResult Process(Job job)
    {
        CapturedFrame frame;
        try
        {
            frame = _source.Capture(job.Target);
        }
        catch (Exception e)
        {
            return new CaptureResult(false, null, null, "Capture failed: " + e.Message);
        }

        try
        {
            var names = CapturePairWriter.Write(job.Folder, job.Timestamp, frame);
            return new CaptureResult(true, names.ColorPath, names.GrayPath, null);
        }
        catch (UnauthorizedAccessException e)
        {
            return new CaptureResult(false, null, null, "Folder not writable: " + e.Message);
        }
        catch (IOException e) when (e.HResult is unchecked((int)0x80070070) or unchecked((int)0x80070027))
        {
            return new CaptureResult(false, null, null, "Disk full: " + e.Message);
        }
        catch (Exception e)
        {
            return new CaptureResult(false, null, null, "Save failed: " + e.Message);
        }
    }

    private void RaiseCompleted(CaptureResult result)
    {
        try { CaptureCompleted?.Invoke(result); } catch { }
    }

    /// <summary>Stops accepting requests, finishes queued ones, then releases the source.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        _worker.Join(TimeSpan.FromSeconds(15));
        _queue.Dispose();
        _source.Dispose();
    }
}
