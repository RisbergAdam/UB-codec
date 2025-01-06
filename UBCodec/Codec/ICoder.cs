using System.Collections;
using SkiaSharp;

namespace UBCodec.Codec;

public interface ICoder
{

    public BitArray Encode(SKBitmap input);
    
    public SKBitmap Decode(BitArray input, SKImageInfo imageInfo);

}