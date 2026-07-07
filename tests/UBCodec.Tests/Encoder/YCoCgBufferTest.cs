using SkiaSharp;
using UBCodec.Core.Encoder;
using UBCodec.Core.Utils;
using static UBCodec.Core.Utils.ImageUtils;

namespace UBCodec.Tests.Encoder;

public class YCoCgBufferTest
{
    [Test]
    public void ConversionTest()
    {
        var image = BlockResize(ReadPng("../../../../resources/ezgif-6-e3be30c4ce-png-split/ezgif-frame-001.png"), 16);
        WritePng(YCoCgBuffer.FromBitmap(image).ToBitmap(), "../../../artifacts/conversion-test.png");
    }
}