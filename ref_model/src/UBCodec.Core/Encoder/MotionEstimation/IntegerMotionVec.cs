using System.Numerics;
using CommunityToolkit.HighPerformance;

namespace UBCodec.Core.Encoder.MotionEstimation;

public class IntegerMotionVec : IMotionEstimator
{
    public EstimatedMotion Estimate(Span2D<byte> source, Span2D<byte> template)
    {
        // The buffers are byte[width, height] indexed [x, y] with x = first index.
        // Span2D maps span[a, b] == array[a, b], so the FIRST span index is x.
        // Keep the same convention as IntegerMotionRef: template(x, y) vs source(x + dx, y + dy).
        int padX = source.Height - template.Height; // search range for the x (first) index
        int padY = source.Width - template.Width;   // search range for the y (second) index
        long n = (long)template.Width * template.Height;

        long errorBest = long.MaxValue;
        int xBest = 0;
        int yBest = 0;

        if (padX < 0 || padY < 0)
            return default;

        // Vector<byte>.Count will dynamically be 16 on ARM64 (NEON) and 32 on x64 (AVX2)
        int vecSize = Vector<byte>.Count;

        for (int dx = 0; dx <= padX; dx++)
        for (int dy = 0; dy <= padY; dy++)
        {
            long sad = 0;

            // The first array index is x; the second index (y) is contiguous in memory,
            // so GetRowSpan(x + dx) is the vertical line at x + dx and we slide along y.
            for (int x = 0; x < template.Height; x++)
            {
                ReadOnlySpan<byte> srcCol = source.GetRowSpan(x + dx);
                ReadOnlySpan<byte> tmpCol = template.GetRowSpan(x);

                int y = 0;
                int height = template.Width;

                // Unified Hardware-Accelerated SIMD loop
                if (Vector.IsHardwareAccelerated && height >= vecSize)
                {
                    // Accumulate into ushorts to prevent byte overflow (max 255)
                    Vector<ushort> acc = Vector<ushort>.Zero;

                    for (; y <= height - vecSize; y += vecSize)
                    {
                        // Slice to the current offset, the JIT converts this directly into unaligned vector loads (vld1q_u8 / vmovdqu)
                        var vSrc = new Vector<byte>(srcCol.Slice(y + dy));
                        var vTmp = new Vector<byte>(tmpCol.Slice(y));

                        // Cross-platform Absolute Difference trick for unsigned bytes: Max(a, b) - Min(a, b)
                        var max = Vector.Max(vSrc, vTmp);
                        var min = Vector.Min(vSrc, vTmp);
                        var diff = Vector.Subtract(max, min);

                        // Widen the 8-bit differences into 16-bit to safely add them up
                        Vector.Widen(diff, out Vector<ushort> diffLow, out Vector<ushort> diffHigh);

                        acc = Vector.Add(acc, diffLow);
                        acc = Vector.Add(acc, diffHigh);
                    }

                    sad += Vector.Sum(acc);
                }

                // Scalar fallback handles remaining pixels (e.g., if height is not a multiple of the vector size)
                for (; y < height; y++)
                {
                    sad += Math.Abs(tmpCol[y] - srcCol[y + dy]);
                }

                if (sad >= errorBest)
                {
                    break;
                }
            }

            if (sad < errorBest)
            {
                errorBest = sad;
                xBest = dx;
                yBest = dy;
            }
        }

        return new EstimatedMotion {
            X = xBest,
            Y = yBest,
            Error = (float)errorBest / n
        };
    }
}