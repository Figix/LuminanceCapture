namespace LuminanceCapture.Core;

/// <summary>The monitor chosen at the moment of the key press.</summary>
public readonly record struct CaptureTarget(IntPtr Monitor);

/// <summary>Tightly packed BGRA32 pixels (stride = Width * 4), alpha 255.</summary>
public sealed record CapturedFrame(byte[] Bgra, int Width, int Height);

public interface ICaptureSource : IDisposable
{
    CapturedFrame Capture(CaptureTarget target);
}

public sealed record CaptureResult(bool Success, string? ColorPath, string? GrayPath, string? Error);
