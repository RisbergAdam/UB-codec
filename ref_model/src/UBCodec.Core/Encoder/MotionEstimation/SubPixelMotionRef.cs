using CommunityToolkit.HighPerformance;
using UBCodec.Core.Encoder.Sampling;

namespace UBCodec.Core.Encoder.MotionEstimation;

public class SubPixelMotionRef : IMotionEstimator
{
    private IntegerMotionRef _imotion = new();
    
    public EstimatedMotion Estimate(Span2D<byte> source, Span2D<byte> template)
    {
        var intEstimate = _imotion.Estimate(source, template);
        
        // compute gradients
        var size = template.Height;
        
        var Ix = new int[size-2, size-2];
        var Iy = new int[size-2, size-2];
        var It = new int[size-2, size-2];

        for (var y = 0; y < size - 2; y++)
        for (var x = 0; x < size - 2; x++)
        {
            var x1 = 1 + x; var y1 = 1 + y;
            Ix[x, y] = (template[x1 - 1, y1] - template[x1 + 1, y1])/2;
            Iy[x, y] = (template[x1, y1 - 1] - template[x1, y1 + 1])/2;
            It[x, y] = template[x1, y1] - source[x1 + (int) intEstimate.X, y1 + (int) intEstimate.Y];
        }
        
        // accumulate gradients
        float Sxx = 0.0f;
        float Syy = 0.0f;
        float Sxy = 0.0f;
        float Sxt = 0.0f;
        float Syt = 0.0f;

        // 1. Accumulate gradient products over the N x N block
        for (int y = 0; y < size-2; y++)
        for (int x = 0; x < size-2; x++)
        {
            float ix = Ix[x, y];
            float iy = Iy[x, y];
            float it = It[x, y];

            Sxx += ix * ix;
            Syy += iy * iy;
            Sxy += ix * iy;
            Sxt += ix * it;
            Syt += iy * it;
        }

        // 2. Compute the determinant of the 2x2 ATA matrix
        float det = (Sxx * Syy) - (Sxy * Sxy);

        if (det < 0.001f)
        {
            return intEstimate;
        }
        
        float invDet = 1.0f / det;

        float deltaX = (-Syy * Sxt + Sxy * Syt) * invDet;
        float deltaY = ( Sxy * Sxt - Sxx * Syt) * invDet;

        // 5. Clamp the result: fractional offset from integer match should be within [-1.0, +1.0]
        deltaX = Math.Clamp(deltaX, -1.0f, 1.0f);
        deltaY = Math.Clamp(deltaY, -1.0f, 1.0f);
        
        float quarterPelX = MathF.Round(deltaX * 4.0f) / 4.0f;
        float quarterPelY = MathF.Round(deltaY * 4.0f) / 4.0f;
        
        var em = new EstimatedMotion
        {
            X = intEstimate.X + quarterPelX,
            Y = intEstimate.Y + quarterPelY,
        };

        ComputeError(em, source.ToArray(), template.ToArray());

        return em;
    }

    private void ComputeError(EstimatedMotion motion, byte[,] source, byte[,] template)
    {
        var size = template.GetLength(0);
        
        var template2 = SubpixelSampler.Crop(source, motion.X, motion.Y, size);
        
        var error = 0f;
        
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            error += Math.Abs(template[x, y] - template2[x, y]);
        }

        motion.Error = error;
    }
}