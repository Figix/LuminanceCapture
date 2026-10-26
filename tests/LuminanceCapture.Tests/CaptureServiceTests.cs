using System.Windows.Media;
using System.Windows.Media.Imaging;
using LuminanceCapture.Core;

namespace LuminanceCapture.Tests;

public sealed class CaptureServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lc-cap-" + Guid.NewGuid().ToString("N"));

    public CaptureServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private sealed class FakeSource : ICaptureSource
    {
        public Func<CaptureTarget, CapturedFrame> Impl { get; set; }
        public FakeSource(Func<CaptureTarget, CapturedFrame> impl) => Impl = impl;
        public CapturedFrame Capture(CaptureTarget target) => Impl(target);
        public void Dispose() { }
    }

    private static CapturedFrame MakeFrame(int w = 5, int h = 3, int seed = 1)
    {
        var rng = new Random(seed);
        var px = new byte[w * h * 4];
        rng.NextBytes(px);
        for (int i = 3; i < px.Length; i += 4) px[i] = 255;
        return new CapturedFrame(px, w, h);
    }

    private static byte[] DecodeBgra(string path, out int w, out int h, out PixelFormat format)
    {
        using var fs = File.OpenRead(path);
        var frame = new PngBitmapDecoder(fs, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        format = frame.Format;
        var conv = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        w = conv.PixelWidth; h = conv.PixelHeight;
        var buf = new byte[w * h * 4];
        conv.CopyPixels(buf, w * 4, 0);
        return buf;
    }

    private static CaptureResult RunOne(CaptureService svc, Action request)
    {
        CaptureResult? result = null;
        using var done = new ManualResetEventSlim();
        svc.CaptureCompleted += r => { result = r; done.Set(); };
        request();
        Assert.True(done.Wait(10000));
        return result!;
    }

    [Fact]
    public void Pair_ColorEqualsSource_GrayIsLuminanceOfColor_AndPngDecodesLossless()
    {
        var frame = MakeFrame(7, 4, 42);
        using var svc = new CaptureService(new FakeSource(_ => frame), () => _root, () => new CaptureTarget(1));
        var r = RunOne(svc, svc.Request);

        Assert.True(r.Success);
        var color = DecodeBgra(r.ColorPath!, out int w, out int h, out var fmt);
        Assert.Equal((7, 4), (w, h));
        Assert.Equal(PixelFormats.Bgr24, fmt);
        Assert.Equal(frame.Bgra, color);

        var expectedGray = new byte[frame.Bgra.Length];
        Luminance.ToGrayscale(frame.Bgra, expectedGray);
        var gray = DecodeBgra(r.GrayPath!, out _, out _, out var gfmt);
        Assert.Equal(PixelFormats.Bgr24, gfmt);
        Assert.Equal(expectedGray, gray);
        for (int i = 0; i < gray.Length; i += 4)
            Assert.True(gray[i] == gray[i + 1] && gray[i + 1] == gray[i + 2]);
    }

    [Fact]
    public void Pair_SharesStem_AndSkipsExistingFiles()
    {
        var ts = new DateTime(2026, 1, 2, 3, 4, 5);
        var first = PairNameAllocator.Allocate(_root, ts);
        File.WriteAllText(first.ColorPath, "keep");

        var names = CapturePairWriter.Write(_root, ts, MakeFrame());

        Assert.EndsWith("2026-01-02_030405_2_color.png", names.ColorPath);
        Assert.EndsWith("2026-01-02_030405_2_gray.png", names.GrayPath);
        Assert.Equal("keep", File.ReadAllText(first.ColorPath));
        Assert.False(File.Exists(first.GrayPath));
    }

    [Fact]
    public void Failure_OnSecondRename_LeavesNoFiles_AndPreExistingUntouched()
    {
        var ts = new DateTime(2026, 1, 2, 3, 4, 5);
        var other = Path.Combine(_root, "other.txt");
        File.WriteAllText(other, "x");
        // A directory squatting on the gray target makes the second rename fail after the first succeeded.
        Directory.CreateDirectory(Path.Combine(_root, "2026-01-02_030405_gray.png"));

        Assert.ThrowsAny<Exception>(() => CapturePairWriter.Write(_root, ts, MakeFrame()));

        var files = Directory.GetFiles(_root).Select(Path.GetFileName).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "other.txt" }, files);
        Assert.Equal("x", File.ReadAllText(other));
    }

    [Fact]
    public void Service_ReportsFailure_WhenSaveFails()
    {
        var blocker = Path.Combine(_root, "file");
        File.WriteAllText(blocker, "x");
        using var svc = new CaptureService(new FakeSource(_ => MakeFrame()), () => Path.Combine(blocker, "sub"), () => new CaptureTarget(1));
        var r = RunOne(svc, svc.Request);

        Assert.False(r.Success);
        Assert.Null(r.ColorPath);
        Assert.False(string.IsNullOrWhiteSpace(r.Error));
    }

    [Fact]
    public void Service_ReportsFailure_WhenCaptureThrows()
    {
        using var svc = new CaptureService(new FakeSource(_ => throw new InvalidOperationException("boom")), () => _root, () => new CaptureTarget(1));
        var r = RunOne(svc, svc.Request);

        Assert.False(r.Success);
        Assert.Contains("boom", r.Error);
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public void Service_CreatesMissingFolder()
    {
        var folder = Path.Combine(_root, "a", "b");
        using var svc = new CaptureService(new FakeSource(_ => MakeFrame()), () => folder, () => new CaptureTarget(1));
        var r = RunOne(svc, svc.Request);

        Assert.True(r.Success);
        Assert.True(File.Exists(r.ColorPath));
        Assert.True(File.Exists(r.GrayPath));
    }

    [Fact]
    public void Service_RapidRequests_AllProduceUniquePairs_InOrder()
    {
        var order = new List<int>();
        int next = 0;
        var source = new FakeSource(t =>
        {
            order.Add((int)t.Monitor);
            Thread.Sleep(15);
            return MakeFrame(3, 2, (int)t.Monitor);
        });
        var results = new List<CaptureResult>();
        using var done = new CountdownEvent(20);
        using var svc = new CaptureService(source, () => _root, () => new CaptureTarget(++next));
        svc.CaptureCompleted += r => { lock (results) results.Add(r); done.Signal(); };

        for (int i = 0; i < 20; i++) svc.Request();
        Assert.True(done.Wait(30000));

        Assert.Equal(Enumerable.Range(1, 20), order);
        Assert.All(results, r => Assert.True(r.Success, r.Error));
        Assert.Equal(20, results.Select(r => r.ColorPath).Distinct().Count());
        Assert.Equal(40, Directory.GetFiles(_root, "*.png").Length);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        // Completion order matches request order: file i holds frame seeded with i.
        for (int i = 0; i < 20; i++)
        {
            var px = DecodeBgra(results[i].ColorPath!, out _, out _, out _);
            Assert.Equal(MakeFrame(3, 2, i + 1).Bgra, px);
        }
    }

    [Fact]
    public void Service_FolderProviderIsEvaluatedPerRequest()
    {
        var f1 = Path.Combine(_root, "one");
        var f2 = Path.Combine(_root, "two");
        var current = f1;
        using var svc = new CaptureService(new FakeSource(_ => MakeFrame()), () => current, () => new CaptureTarget(1));
        var r1 = RunOne(svc, svc.Request);
        current = f2;
        var r2 = RunOne(svc, svc.Request);

        Assert.StartsWith(f1, r1.ColorPath);
        Assert.StartsWith(f2, r2.ColorPath);
    }

    [Fact]
    public void Dispose_DrainsQueuedRequests()
    {
        var count = 0;
        var svc = new CaptureService(new FakeSource(_ => { Thread.Sleep(20); return MakeFrame(); }), () => _root, () => new CaptureTarget(1));
        svc.CaptureCompleted += _ => Interlocked.Increment(ref count);
        for (int i = 0; i < 5; i++) svc.Request();
        svc.Dispose();

        Assert.Equal(5, count);
    }
}
