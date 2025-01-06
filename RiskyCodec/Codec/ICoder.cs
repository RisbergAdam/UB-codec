using System.Collections;
using SkiaSharp;

namespace RiskyCodec.Codec;

public interface ICoder
{

    public BitArray Encode(SKBitmap input);
    
    public SKBitmap Decode(BitArray input, SKImageInfo imageInfo);

}