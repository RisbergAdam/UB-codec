using System;
using System.Numerics;
using CommunityToolkit.HighPerformance;

namespace UBCodec.Core.Encoder.MotionEstimation;

public class IntegerMotionVec : IMotionEstimator
{
    public EstimatedMotion Estimate(Span2D<byte> source, Span2D<byte> template)
    {
        int paddingX = source.Width - template.Width;
        int paddingY = source.Height - template.Height;
        long n = (long)template.Width * template.Height;

        long errorBest = long.MaxValue;
        int xBest = 0;
        int yBest = 0;

        if (paddingX < 0 || paddingY < 0)
            return default;
            
        // Vector<byte>.Count will dynamically be 16 on ARM64 (NEON) and 32 on x64 (AVX2)
        int vecSize = Vector<byte>.Count; 

        for (int dy = 0; dy <= paddingY; dy++)
        {
            for (int dx = 0; dx <= paddingX; dx++)
            {
                long sad = 0;

                for (int y = 0; y < template.Height; y++)
                {
                    ReadOnlySpan<byte> srcRow = source.GetRowSpan(y + dy);
                    ReadOnlySpan<byte> tmpRow = template.GetRowSpan(y);

                    int x = 0;
                    int width = template.Width;

                    // Unified Hardware-Accelerated SIMD loop
                    if (Vector.IsHardwareAccelerated && width >= vecSize)
                    {
                        // Accumulate into ushorts to prevent byte overflow (max 255)
                        Vector<ushort> acc = Vector<ushort>.Zero;

                        for (; x <= width - vecSize; x += vecSize)
                        {
                            // Slice to the current offset, the JIT converts this directly into unaligned vector loads (vld1q_u8 / vmovdqu)
                            var vSrc = new Vector<byte>(srcRow.Slice(x + dx));
                            var vTmp = new Vector<byte>(tmpRow.Slice(x));

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

                    // Scalar fallback handles remaining pixels (e.g., if width is not a multiple of the vector size)
                    for (; x < width; x++)
                    {
                        sad += Math.Abs(tmpRow[x] - srcRow[x + dx]);
                    }

                    // Early termination: HUGE speed boost.
                    // If this block is already worse than our best, bail out immediately.
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
        }

        return new EstimatedMotion {
            X = yBest,
            Y = xBest,
            Error = (float)errorBest / n
        };
    }
}