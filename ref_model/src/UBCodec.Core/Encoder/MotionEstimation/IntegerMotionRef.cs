using CommunityToolkit.HighPerformance;

namespace UBCodec.Core.Encoder.MotionEstimation;

public class IntegerMotionRef : IMotionEstimator
{
    public EstimatedMotion Estimate(Span2D<byte> source, Span2D<byte> template)
    {
        var padding = source.Height - template.Height;
        var templateSize = template.Height;
        var n = (long)templateSize * templateSize;

        long errorBest = long.MaxValue;
        var xBest = 0;
        var yBest = 0;

        for (var dx = 0; dx <= padding; dx++)
        for (var dy = 0; dy <= padding; dy++)
        {
            var sad = 0L;

            for (var y = 0; y < templateSize; y++)
            for (var x = 0; x < templateSize; x++)
            {
                sad += Math.Abs(template[x, y] - source[x + dx, y + dy]);
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