using LuminanceCapture.Core;

namespace LuminanceCapture.Tests;

public class LuminanceTests
{
    private static byte[] Px(byte r, byte g, byte b, byte a = 255) => new[] { b, g, r, a };

    [Theory]
    [InlineData(255, 0, 0, 54)]
    [InlineData(0, 255, 0, 182)]
    [InlineData(0, 0, 255, 18)]
    [InlineData(255, 255, 255, 255)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(128, 128, 128, 128)]
    public void KnownColors(byte r, byte g, byte b, byte expected)
    {
        var buf = Px(r, g, b, 200);
        Luminance.ToGrayscale(buf);
        Assert.Equal(new[] { expected, expected, expected, (byte)200 }, buf);
    }

    [Fact]
    public void GrayInputUnchangedForEveryValue()
    {
        var buf = new byte[256 * 4];
        for (int v = 0; v < 256; v++)
        {
            buf[v * 4] = buf[v * 4 + 1] = buf[v * 4 + 2] = (byte)v;
            buf[v * 4 + 3] = 255;
        }
        var expected = (byte[])buf.Clone();
        Luminance.ToGrayscale(buf);
        Assert.Equal(expected, buf);
    }

    [Fact]
    public void CopyOverloadLeavesSourceIntact()
    {
        var src = Px(255, 0, 0);
        var dst = new byte[4];
        Luminance.ToGrayscale(src, dst);
        Assert.Equal(Px(255, 0, 0), src);
        Assert.Equal(new byte[] { 54, 54, 54, 255 }, dst);
    }

    [Fact]
    public void RejectsLengthNotMultipleOfFour()
    {
        Assert.Throws<ArgumentException>(() => Luminance.ToGrayscale(new byte[5]));
    }

    [Fact]
    public void MatrixColumnsHoldTheSharedCoefficients()
    {
        var m = Luminance.CreateMagColorMatrix();
        Assert.Equal(25, m.Length);
        for (int col = 0; col < 3; col++)
        {
            Assert.Equal((float)Luminance.CoefficientR, m[0 * 5 + col]);
            Assert.Equal((float)Luminance.CoefficientG, m[1 * 5 + col]);
            Assert.Equal((float)Luminance.CoefficientB, m[2 * 5 + col]);
            Assert.Equal(0f, m[3 * 5 + col]);
            Assert.Equal(0f, m[4 * 5 + col]);
        }
        Assert.Equal(1f, m[3 * 5 + 3]);
        Assert.Equal(1f, m[4 * 5 + 4]);
        Assert.Equal(0f, m[0 * 5 + 3]);
        Assert.Equal(0f, m[0 * 5 + 4]);
    }

    [Theory]
    [InlineData(255, 0, 0)]
    [InlineData(10, 200, 30)]
    [InlineData(77, 33, 250)]
    [InlineData(1, 2, 3)]
    public void MatrixMathMatchesBufferFunction(byte r, byte g, byte b)
    {
        var m = Luminance.CreateMagColorMatrix();
        float[] v = { r, g, b, 255, 1 };
        float y = 0;
        for (int i = 0; i < 5; i++) y += v[i] * m[i * 5 + 0];
        var buf = Px(r, g, b);
        Luminance.ToGrayscale(buf);
        Assert.InRange(Math.Abs(y - buf[0]), 0, 0.5001);
    }
}
