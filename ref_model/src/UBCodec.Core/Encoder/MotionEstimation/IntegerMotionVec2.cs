using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using CommunityToolkit.HighPerformance;

namespace UBCodec.Core.Encoder.MotionEstimation;

public class IntegerMotionVec2 : IMotionEstimator
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

        for (int dy = 0; dy <= paddingY; dy++)
        for (int dx = 0; dx <= paddingX; dx++)
        {
            long sad = 0;

            for (int y = 0; y < template.Height; y++)
            {
                ReadOnlySpan<byte> srcRow = source.GetRowSpan(y + dy);
                ReadOnlySpan<byte> tmpRow = template.GetRowSpan(y);

                ref byte pSrc = ref MemoryMarshal.GetReference(srcRow);
                ref byte pTmp = ref MemoryMarshal.GetReference(tmpRow);

                int x = 0;
                int width = template.Width;

                // Apple Silicon (ARM64 NEON) Fast Path
                if (AdvSimd.Arm64.IsSupported)
                {
                    var acc1 = Vector128<ushort>.Zero;
                    var acc2 = Vector128<ushort>.Zero;

                    // Process 32 bytes per iteration
                    for (; x <= width - 32; x += 32)
                    {
                        var vSrc1 = Vector128.LoadUnsafe(ref pSrc, (nuint)(x + dx));
                        var vTmp1 = Vector128.LoadUnsafe(ref pTmp, (nuint)x);
                        var diff1 = AdvSimd.AbsoluteDifference(vSrc1, vTmp1);
                        
                        // Widen bytes -> ushorts (vpaddlq_u8) to prevent 8-bit overflow
                        var w1 = AdvSimd.AddPairwiseWidening(diff1);
                        acc1 = AdvSimd.Add(acc1, w1);

                        var vSrc2 = Vector128.LoadUnsafe(ref pSrc, (nuint)(x + dx + 16));
                        var vTmp2 = Vector128.LoadUnsafe(ref pTmp, (nuint)(x + 16));
                        var diff2 = AdvSimd.AbsoluteDifference(vSrc2, vTmp2);
                        
                        var w2 = AdvSimd.AddPairwiseWidening(diff2);
                        acc2 = AdvSimd.Add(acc2, w2);
                    }

                    // Handle 16-byte remainder
                    if (x <= width - 16)
                    {
                        var vSrc = Vector128.LoadUnsafe(ref pSrc, (nuint)(x + dx));
                        var vTmp = Vector128.LoadUnsafe(ref pTmp, (nuint)x);
                        var diff = AdvSimd.AbsoluteDifference(vSrc, vTmp);
                        
                        var w = AdvSimd.AddPairwiseWidening(diff);
                        acc1 = AdvSimd.Add(acc1, w);
                        x += 16;
                    }

                    // Reduce vector accumulators to scalar ushort and add to long
                    var combinedAcc = AdvSimd.Add(acc1, acc2);
                    sad += AdvSimd.Arm64.AddAcross(combinedAcc).ToScalar();
                }

                // Scalar fallback using Spans directly (fixes indexing issue)
                for (; x < width; x++)
                {
                    sad += Math.Abs(tmpRow[x] - srcRow[x + dx]);
                }

                // Early termination
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