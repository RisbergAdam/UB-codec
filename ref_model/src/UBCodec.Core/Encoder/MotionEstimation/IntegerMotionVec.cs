using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using CommunityToolkit.HighPerformance;

namespace UBCodec.Core.Encoder.MotionEstimation;

public class IntegerMotionVec : IMotionEstimator
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public EstimatedMotion Estimate(Span2D<byte> source, Span2D<byte> template)
    {
        var templateSize = template.Height;
        var padding = source.Height - templateSize;

        long errorBest = long.MaxValue;
        var xBest = 0;
        var yBest = 0;

        for (var dx = 0; dx <= padding; dx++)
        {
            for (var dy = 0; dy <= padding; dy++)
            {
                long sse = Sse128(source, template, dx, dy, templateSize, errorBest);

                if (sse < errorBest) // same tie-breaking as reference
                {
                    errorBest = sse;
                    xBest = dx;
                    yBest = dy;
                }
            }
        }

        var n = (long)templateSize * templateSize;
        return new EstimatedMotion { X = xBest, Y = yBest, Error = (float)errorBest / n };
    }

    private static long Sse128(Span2D<byte> source, Span2D<byte> template, int dx, int dy, int size, long errorBest)
    {
        var acc = Vector128<int>.Zero;

        for (var x = 0; x < size; x++)
        {
            ReadOnlySpan<byte> srcRow = source.GetRowSpan(dy + x);
            ReadOnlySpan<byte> tmpRow = template.GetRowSpan(x);
            
            ref var s = ref MemoryMarshal.GetReference(srcRow.Slice(dx));
            ref var t = ref MemoryMarshal.GetReference(tmpRow);

            for (var y = 0; y < size; y += Vector128<byte>.Count)
            {
                var sv = Vector128.LoadUnsafe(in Unsafe.Add(ref s, y));
                var tv = Vector128.LoadUnsafe(in Unsafe.Add(ref t, y));

                var (s0, s1) = Vector128.Widen(sv);
                var (t0, t1) = Vector128.Widen(tv);

                var d0 = s0.AsInt16() - t0.AsInt16();
                var d1 = s1.AsInt16() - t1.AsInt16();
                
                var sq0 = Vector128.Multiply(d0, d0);
                var sq1 = Vector128.Multiply(d1, d1);
                
                var (p0Low, p0High) = Vector128.Widen(sq0);
                var (p1Low, p1High) = Vector128.Widen(sq1);
                
                acc += p0Low.AsInt32() + p0High.AsInt32() + p1Low.AsInt32() + p1High.AsInt32();
            }

            if (Vector128.Sum(acc) > errorBest) return Vector128.Sum(acc);
        }
        
        

        return Vector128.Sum(acc);
    }
}