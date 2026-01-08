using SkiaSharp;
using UBCodec.Codec;

namespace UBCodec.Test.Codec;

using static UBCodec.Codec.ImageUtils;

public class YCoCgBufferTest
{
    [Test]
    public void ConversionTest()
    {
        var image = BlockResize(ReadPng("../../../../UBCodec/resources/ezgif-6-e3be30c4ce-png-split/ezgif-frame-001.png"), 16);
        WritePng(YCoCgBuffer.FromBitmap(image).ToBitmap(), "../../../output/conversion-test.png");
    }
}