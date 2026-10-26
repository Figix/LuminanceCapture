namespace LuminanceCapture.Core;

public static class Luminance
{
    // Rec.709 weights applied directly to gamma-encoded sRGB values (no linearization, by design).
    public const double CoefficientR = 0.2126;
    public const double CoefficientG = 0.7152;
    public const double CoefficientB = 0.0722;

    public static double Compute(byte r, byte g, byte b) =>
        CoefficientR * r + CoefficientG * g + CoefficientB * b;

    public static byte ComputeByte(byte r, byte g, byte b) =>
        (byte)Math.Clamp((int)Math.Floor(Compute(r, g, b) + 0.5), 0, 255);

    // MAGCOLOREFFECT.transform: 5x5 row-major, color row vector [R G B A 1] * M.
    // Output channel j is column j, so columns 0..2 each hold (CoefficientR, CoefficientG, CoefficientB).
    public static float[] CreateMagColorMatrix()
    {
        var m = new float[25];
        for (int col = 0; col < 3; col++)
        {
            m[0 * 5 + col] = (float)CoefficientR;
            m[1 * 5 + col] = (float)CoefficientG;
            m[2 * 5 + col] = (float)CoefficientB;
        }
        m[3 * 5 + 3] = 1f;
        m[4 * 5 + 4] = 1f;
        return m;
    }

    // BGRA32 in place: R=G=B=round(Y), alpha untouched.
    public static void ToGrayscale(Span<byte> bgra) => ToGrayscale(bgra, bgra);

    public static void ToGrayscale(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length % 4 != 0)
            throw new ArgumentException("BGRA32 buffer length must be a multiple of 4.", nameof(source));
        if (destination.Length < source.Length)
            throw new ArgumentException("Destination is smaller than source.", nameof(destination));

        for (int i = 0; i < source.Length; i += 4)
        {
            byte y = ComputeByte(source[i + 2], source[i + 1], source[i]);
            byte a = source[i + 3];
            destination[i] = y;
            destination[i + 1] = y;
            destination[i + 2] = y;
            destination[i + 3] = a;
        }
    }
}
