namespace UBCodec.Core.Encoder;

public struct MotionEstimate
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Error { get; set; }
}

public interface IBlockMotionEstimator
{
    public MotionEstimate EstimateMotion(byte[,] block, byte[,] blockPrev);
}

public class NoopMotionEstimator : IBlockMotionEstimator
{
    public MotionEstimate EstimateMotion(byte[,] block, byte[,] blockPrev)
    {
        return new MotionEstimate
        {
            X = 0,
            Y = 0,
            Error = 0,
        };
    }
}