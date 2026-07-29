using CommunityToolkit.HighPerformance;

namespace UBCodec.Core.Encoder.ImageTransform;

public interface ITransform8X8
{
    public void Forward(Span2D<byte> input, Span2D<int> output);
    
    public void Inverse(Span2D<int> input, Span2D<byte> output);
}