using System.Numerics;
using CommunityToolkit.HighPerformance;
using UBCodec.Core.Encoder;

namespace UBCodec.Core.Utils;

/// <summary>
/// Per-frame rate/distortion metrics for an encode. Pure data — computed by
/// <see cref="Distortion.CollectMetrics"/>.
/// </summary>
public readonly record struct FrameMetrics
{
    /// <summary>PSNR in dB of the luma plane.</summary>
    public double LumaPsnr { get; init; }

    /// <summary>4:2:0-weighting: (6*Luma + C1 + C2) / 8.</summary>
    public double WeightedPsnr { get; init; }

    /// <summary>Bits per pixel used to encode this frame.</summary>
    public double Bpp { get; init; }

    /// <summary>True when every plane is bit-identical (PSNR = +Infinity).</summary>
    public bool Lossless => double.IsPositiveInfinity(LumaPsnr)
                            && double.IsPositiveInfinity(WeightedPsnr);
}

/// <summary>
/// Distortion measurement (MSE / PSNR) for reconstructed frames, using
/// hardware-accelerated SIMD (Vector&lt;byte&gt;) the same way the motion
/// estimators do. Buffers are byte[width, height] indexed [x, y], so a
/// "column" (GetRowSpan(x)) is the contiguous memory to slide over.
/// </summary>
public static class Distortion
{
    /// <summary>
    /// Sum of squared differences between two equal-length byte spans,
    /// computed with vector instructions.
    /// </summary>
    public static long SumSquaredDiff(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Spans must have equal length.", nameof(b));

        long result = 0;
        int vecSize = Vector<byte>.Count;

        if (Vector.IsHardwareAccelerated && a.Length >= vecSize)
        {
            // Each vector-add contributes up to 4 * 255^2 = 260100, so flush into
            // the long accumulator periodically to avoid uint overflow on long spans.
            int flushEvery = 2048;
            int i = 0;
            Vector<uint> acc = Vector<uint>.Zero;

            for (; i <= a.Length - vecSize; i += vecSize)
            {
                var va = new Vector<byte>(a.Slice(i));
                var vb = new Vector<byte>(b.Slice(i));

                // Cross-platform absolute difference: Max - Min.
                var diff = Vector.Subtract(Vector.Max(va, vb), Vector.Min(va, vb));

                // Widen bytes -> ushort, then ushort -> uint so squaring 255^2 fits.
                Vector.Widen(diff, out Vector<ushort> d0, out Vector<ushort> d1);
                Vector.Widen(d0, out Vector<uint> d0Lo, out Vector<uint> d0Hi);
                Vector.Widen(d1, out Vector<uint> d1Lo, out Vector<uint> d1Hi);

                acc = Vector.Add(acc, d0Lo * d0Lo);
                acc = Vector.Add(acc, d0Hi * d0Hi);
                acc = Vector.Add(acc, d1Lo * d1Lo);
                acc = Vector.Add(acc, d1Hi * d1Hi);

                if (((i / vecSize) & (flushEvery - 1)) == 0)
                {
                    result += Vector.Sum(acc);
                    acc = Vector<uint>.Zero;
                }
            }

            result += Vector.Sum(acc);

            // Scalar tail for remaining elements.
            for (; i < a.Length; i++)
            {
                int d = a[i] - b[i];
                result += (long)d * d;
            }

            return result;
        }

        // Fully scalar fallback (non-hardware-accelerated tiny spans).
        for (int i = 0; i < a.Length; i++)
        {
            int d = a[i] - b[i];
            result += (long)d * d;
        }

        return result;
    }

    /// <summary>Mean squared error between two same-sized byte planes.</summary>
    public static double MSE(byte[,] a, byte[,] b)
    {
        if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1))
            throw new ArgumentException("Planes must have identical dimensions.");

        int width = a.GetLength(0);
        int height = a.GetLength(1);

        var sa = new Span2D<byte>(a);
        var sb = new Span2D<byte>(b);

        long ssd = 0;
        for (int x = 0; x < width; x++)
        {
            ssd += SumSquaredDiff(sa.GetRowSpan(x), sb.GetRowSpan(x));
        }

        return (double)ssd / (width * height);
    }

    /// <summary>PSNR in dB from an MSE value. +Infinity when lossless (MSE == 0).</summary>
    ///
    /// Rough reference scale (8-bit luma):
    ///   PSNR (dB)   Perceived quality
    ///   40+         Very good to lossless (MSE &lt; ~6.5)
    ///   30-40       Good; visible but minor artifacts
    ///   25-30       Fair/poor; clearly lossy, blockiness visible
    ///   &lt; 25        Poor to bad
    ///
    /// Compare only same-source, same-resolution frames; absolute dB values
    /// are meaningless without context.
    public static double PSNR(double mse)
        => mse <= 0 ? double.PositiveInfinity : 10.0 * Math.Log10(255.0 * 255.0 / mse);

    /// <summary>PSNR of the luma plane between two frames.</summary>
    public static double LumaPSNR(PlanarImage a, PlanarImage b)
        => PSNR(MSE(a.LBuffer, b.LBuffer));

    /// <summary>
    /// Collect per-frame rate/distortion metrics comparing an original frame to
    /// its decoded reconstruction. One call yields Bpp plus per-plane PSNR.
    /// </summary>
    public static FrameMetrics CollectMetrics(PlanarImage orig, PlanarImage decoded, int byteCount)
    {
        double lumaPsnr = PSNR(MSE(orig.LBuffer, decoded.LBuffer));
        double c1Psnr = PSNR(MSE(orig.C1Buffer, decoded.C1Buffer));
        double c2Psnr = PSNR(MSE(orig.C2Buffer, decoded.C2Buffer));

        return new FrameMetrics
        {
            LumaPsnr = lumaPsnr,
            WeightedPsnr = (6 * lumaPsnr + c1Psnr + c2Psnr) / 8,
            Bpp = ComputeBpp(byteCount, orig.Width, orig.Height),
        };
    }

    /// <summary>Bits per pixel for a given encoded byte count and frame size.</summary>
    private static double ComputeBpp(int byteCount, int width, int height)
        => (double)byteCount * 8.0 / (width * height);
}
