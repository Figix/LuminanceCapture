using LuminanceCapture.Core;

namespace LuminanceCapture.Tests;

public class GrayscaleFilterTests
{
    private sealed class FakeApi : IColorEffectApi
    {
        public List<float[]> Calls = new();
        public bool Fail;
        public bool Disposed;
        public bool SetMatrix(float[] m) { if (Fail) return false; Calls.Add((float[])m.Clone()); return true; }
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void StartsOff_AndAppliesNothing()
    {
        var api = new FakeApi();
        using var f = new GrayscaleFilter(api);
        Assert.False(f.IsOn);
        Assert.Empty(api.Calls);
    }

    [Fact]
    public void Toggle_AppliesGrayMatrixThenIdentity()
    {
        var api = new FakeApi();
        using var f = new GrayscaleFilter(api);
        Assert.True(f.Toggle());
        Assert.True(f.IsOn);
        Assert.Equal(Luminance.CreateMagColorMatrix(), api.Calls[^1]);
        Assert.True(f.Toggle());
        Assert.False(f.IsOn);
        Assert.Equal(1f, api.Calls[^1][0]);
        Assert.Equal(0f, api.Calls[^1][1]);
    }

    [Fact]
    public void Dispose_RestoresColorAndReleasesApi()
    {
        var api = new FakeApi();
        var f = new GrayscaleFilter(api);
        f.SetOn(true);
        f.Dispose();
        Assert.False(f.IsOn);
        Assert.True(api.Disposed);
        Assert.Equal(1f, api.Calls[^1][0]);
        Assert.Equal(0f, api.Calls[^1][1]);
    }

    [Fact]
    public void Reapply_OnlyWhenOn()
    {
        var api = new FakeApi();
        using var f = new GrayscaleFilter(api);
        f.Reapply();
        Assert.Empty(api.Calls);
        f.SetOn(true);
        var n = api.Calls.Count;
        f.Reapply();
        Assert.Equal(n + 1, api.Calls.Count);
    }

    [Fact]
    public void FailedApply_KeepsStateAndRaisesNoEvent()
    {
        var api = new FakeApi { Fail = true };
        using var f = new GrayscaleFilter(api);
        var raised = 0;
        f.Changed += _ => raised++;
        Assert.False(f.SetOn(true));
        Assert.False(f.IsOn);
        Assert.Equal(0, raised);
    }

    [Fact]
    public void Changed_RaisedOncePerRealChange()
    {
        var api = new FakeApi();
        using var f = new GrayscaleFilter(api);
        var states = new List<bool>();
        f.Changed += states.Add;
        f.SetOn(true); f.SetOn(true); f.SetOn(false);
        Assert.Equal(new[] { true, false }, states);
    }
}
