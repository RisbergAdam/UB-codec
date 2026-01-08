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

    [Test]
    public void ColorSpaceTest()
    {
        int yMin = 999, yMax = -999;
        int coMin = 999, coMax = -999;
        int cgMin = 999, cgMax = -999;
        
        for (var r = 0; r < 256; r += 8)
        for (var g = 0; g < 256; g += 8)
        for (var b = 0; b < 256; b += 8)
        {
            var rgb = new SKColor((byte)r, (byte)g, (byte)b);
            var YCoCg = YCoCgBuffer.ToYCoCgInt(rgb);
            var (y, Co, Cg) = YCoCg;
            yMin = Math.Min(yMin, y);
            yMax = Math.Max(yMax, y);
            coMin = Math.Min(coMin, Co);
            coMax = Math.Max(coMax, Co);
            cgMin = Math.Min(cgMin, Cg);
            cgMax = Math.Max(cgMax, Cg);
            // Console.WriteLine($"({r}, {g}, {b}) ->  {YCoCg}");
        }
        
        Console.WriteLine($"({yMin}, {yMax}, {coMin}, {coMax}, {cgMin}, {cgMax})");
    }
}