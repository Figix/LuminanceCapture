using LuminanceCapture.Core;

namespace LuminanceCapture.Tests;

public class PairNameAllocatorTests
{
    private static readonly DateTime Ts = new(2026, 10, 26, 15, 30, 12);
    private const string Dir = @"C:\shots";

    private static CapturePairNames Alloc(params string[] existing)
    {
        var set = new HashSet<string>(existing.Select(e => Path.Combine(Dir, e)), StringComparer.OrdinalIgnoreCase);
        return PairNameAllocator.Allocate(Dir, Ts, set.Contains);
    }

    [Fact]
    public void NoCollision()
    {
        var p = Alloc();
        Assert.Equal("2026-10-26_153012_color.png", p.ColorFileName);
        Assert.Equal("2026-10-26_153012_gray.png", p.GrayFileName);
        Assert.Equal(Path.Combine(Dir, "2026-10-26_153012_color.png"), p.ColorPath);
        Assert.Equal(Path.Combine(Dir, "2026-10-26_153012_gray.png"), p.GrayPath);
    }

    [Fact]
    public void ColorCollisionOnly()
    {
        var p = Alloc("2026-10-26_153012_color.png");
        Assert.Equal("2026-10-26_153012_2_color.png", p.ColorFileName);
        Assert.Equal("2026-10-26_153012_2_gray.png", p.GrayFileName);
    }

    [Fact]
    public void GrayCollisionOnly()
    {
        var p = Alloc("2026-10-26_153012_gray.png");
        Assert.Equal("2026-10-26_153012_2_color.png", p.ColorFileName);
        Assert.Equal("2026-10-26_153012_2_gray.png", p.GrayFileName);
    }

    [Fact]
    public void SeveralCollisions()
    {
        var p = Alloc("2026-10-26_153012_color.png", "2026-10-26_153012_2_gray.png", "2026-10-26_153012_3_color.png");
        Assert.Equal("2026-10-26_153012_4_color.png", p.ColorFileName);
        Assert.Equal("2026-10-26_153012_4_gray.png", p.GrayFileName);
    }

    [Fact]
    public void DefaultExistenceCheckUsesFileSystem()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lc_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "2026-10-26_153012_gray.png"), "x");
            var p = PairNameAllocator.Allocate(dir, Ts);
            Assert.Equal("2026-10-26_153012_2_color.png", p.ColorFileName);
        }
        finally { Directory.Delete(dir, true); }
    }
}
