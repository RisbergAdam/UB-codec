namespace UBCodec.Codec.NextGen;

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