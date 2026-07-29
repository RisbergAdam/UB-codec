using System.Globalization;
using CommunityToolkit.HighPerformance;

namespace UBCodec.Core.Encoder.MotionEstimation;

public struct EstimatedMotion
{
    public float X;
    public float Y;
    public float Error;

    public override string ToString() =>  $"EstimatedMotion({X.ToString(CultureInfo.InvariantCulture)},{Y.ToString(CultureInfo.InvariantCulture)},Error:{Error.ToString(CultureInfo.InvariantCulture)})";
};

public interface IMotionEstimator
{
    public EstimatedMotion Estimate(Span2D<byte> source, Span2D<byte> template);
}