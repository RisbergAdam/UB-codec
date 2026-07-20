namespace UBCodec.Core.Encoder;

public class BlockMotionEstimatorReference() : IBlockMotionEstimator
{
    public MotionEstimate EstimateMotion(byte[,] block, byte[,] blockPrev)
    {
        var padding = (blockPrev.GetLength(0) - block.GetLength(0)) / 2;
        var D = padding;
        
        var blockSize = block.GetLength(0);
        
        var errorBest = 99999;
        var xBest = 0;
        var yBest = 0;
                
        for (var dx = -D; dx <= D; dx++)
        {
            for (var dy = -D; dy <= D; dy++)
            {
                var error = 0;

                for (var y = 0; y < blockSize; y++)
                {
                    for (var x = 0; x < blockSize; x++)
                    {
                        if (x % 2 > 0 || y % 2 > 0) continue;
                        var curr = block[x, y];
                        var prev = blockPrev[x + dx + D, y + dy + D];
                        error += Math.Abs(curr - prev);
                    }
                }

                if (error < errorBest)
                {
                    errorBest = error;
                    xBest = dx;
                    yBest = dy;
                }
            }
        }

        return new MotionEstimate
        {
            X = xBest,
            Y = yBest,
            Error = errorBest,
        };
    }
}