using SkiaSharp;
using UBCodec.Core.Encoder;
using UBCodec.Core.Utils;
using static UBCodec.Core.Utils.ImageUtils;

namespace UBCodec.Tests.Encoder;

public class PlanarImageTest
{
    private static string _root = Path.GetFullPath("../../../../..");
    
    [Test]
    public void ConversionTest()
    {
        var image = BlockResize(ReadPng(Path.Join(_root, "resources", "drone_frame.png")), 16);
        WritePng(PlanarImage.FromBitmap(image, 2).ToBitmap(), Path.Join(_root, "conversion-test.png"));
    }
}