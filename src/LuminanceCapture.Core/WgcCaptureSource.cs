using System.Diagnostics;
using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

namespace LuminanceCapture.Core;

/// <summary>
/// One-shot Windows.Graphics.Capture grab of a whole monitor. Unlike GDI/DXGI duplication it
/// returns the true color even while the Magnification grayscale filter is active.
/// Not thread-safe; use from a single worker.
/// </summary>
public sealed unsafe class WgcCaptureSource : ICaptureSource
{
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr hstring, ref Guid iid, out IntPtr factory);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string s, int len, out IntPtr h);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr h);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgi, out IntPtr dev);

    private const string ItemClass = "Windows.Graphics.Capture.GraphicsCaptureItem";
    private static readonly Guid IidInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid IidItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
    private static readonly Guid IidDxgiAccess = new("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1");
    private static readonly Guid IidTexture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

    private readonly TimeSpan _frameTimeout;
    private ID3D11Device? _device;
    private ID3D11DeviceContext? _context;
    private IDirect3DDevice? _winrtDevice;

    public WgcCaptureSource(TimeSpan? frameTimeout = null) => _frameTimeout = frameTimeout ?? TimeSpan.FromSeconds(3);

    public CapturedFrame Capture(CaptureTarget target)
    {
        EnsureDevice();
        var item = CreateItem(target.Monitor);
        var size = item.Size;
        using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            _winrtDevice!, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, size);
        using var session = pool.CreateCaptureSession(item);
        try { session.IsCursorCaptureEnabled = false; } catch { }
        try { session.IsBorderRequired = false; } catch { }
        session.StartCapture();

        var sw = Stopwatch.StartNew();
        Direct3D11CaptureFrame? frame;
        while ((frame = pool.TryGetNextFrame()) == null)
        {
            if (sw.Elapsed > _frameTimeout)
                throw new TimeoutException("No frame received from Windows.Graphics.Capture.");
            Thread.Sleep(10);
        }

        using (frame)
            return ReadFrame(frame);
    }

    private static GraphicsCaptureItem CreateItem(IntPtr hmon)
    {
        WindowsCreateString(ItemClass, ItemClass.Length, out var hs);
        IntPtr factory;
        var iidInterop = IidInterop;
        try { Marshal.ThrowExceptionForHR(RoGetActivationFactory(hs, ref iidInterop, out factory)); }
        finally { WindowsDeleteString(hs); }
        try
        {
            var iid = IidItem;
            IntPtr itemPtr;
            // IGraphicsCaptureItemInterop: 0-2 IUnknown, 3 CreateForWindow, 4 CreateForMonitor
            var vt = *(IntPtr**)factory;
            Marshal.ThrowExceptionForHR(
                ((delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)vt[4])(factory, hmon, &iid, &itemPtr));
            return WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPtr);
        }
        finally { Marshal.Release(factory); }
    }

    private void EnsureDevice()
    {
        if (_device != null) return;
        using var dxgiFactory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
        dxgiFactory.EnumAdapters1(0, out IDXGIAdapter1? adapter);
        try
        {
            D3D11.D3D11CreateDevice(adapter, DriverType.Unknown, DeviceCreationFlags.BgraSupport, null,
                out ID3D11Device? dev, out _, out ID3D11DeviceContext? ctx).CheckError();
            _device = dev;
            _context = ctx;
        }
        finally { adapter?.Dispose(); }

        using var dxgiDev = _device!.QueryInterface<IDXGIDevice>();
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDev.NativePointer, out var d3dPtr));
        _winrtDevice = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(d3dPtr);
    }

    private CapturedFrame ReadFrame(Direct3D11CaptureFrame frame)
    {
        IntPtr surfPtr = WinRT.MarshalInterface<IDirect3DSurface>.FromManaged(frame.Surface);
        IntPtr access = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(surfPtr, in IidDxgiAccess, out access));
            var iid = IidTexture2D;
            IntPtr texPtr;
            var vt = *(IntPtr**)access;
            Marshal.ThrowExceptionForHR(
                ((delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)vt[3])(access, &iid, &texPtr));

            using var tex = new ID3D11Texture2D(texPtr);
            var desc = tex.Description;
            desc.Usage = ResourceUsage.Staging;
            desc.CPUAccessFlags = CpuAccessFlags.Read;
            desc.BindFlags = BindFlags.None;
            desc.MiscFlags = ResourceOptionFlags.None;
            using var staging = _device!.CreateTexture2D(desc);
            _context!.CopyResource(staging, tex);

            int width = (int)desc.Width, height = (int)desc.Height;
            var pixels = new byte[checked(width * height * 4)];
            var map = _context.Map(staging, 0, MapMode.Read);
            try
            {
                int rowBytes = width * 4;
                for (int y = 0; y < height; y++)
                {
                    var src = new ReadOnlySpan<byte>((byte*)map.DataPointer + (long)y * map.RowPitch, rowBytes);
                    var dst = pixels.AsSpan(y * rowBytes, rowBytes);
                    src.CopyTo(dst);
                    for (int i = 3; i < rowBytes; i += 4) dst[i] = 255;
                }
            }
            finally { _context.Unmap(staging, 0); }
            return new CapturedFrame(pixels, width, height);
        }
        finally
        {
            if (access != IntPtr.Zero) Marshal.Release(access);
            Marshal.Release(surfPtr);
        }
    }

    public void Dispose()
    {
        (_winrtDevice as IDisposable)?.Dispose();
        _winrtDevice = null;
        _context?.Dispose();
        _context = null;
        _device?.Dispose();
        _device = null;
    }
}
