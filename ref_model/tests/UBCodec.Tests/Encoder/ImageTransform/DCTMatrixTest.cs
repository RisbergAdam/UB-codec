using UBCodec.Core.Encoder;
using UBCodec.Core.Encoder.ImageTransform;

namespace UBCodec.Tests.Encoder.ImageTransform;

public class DCTMatrixTest
{
    [Test]
    public void TestTransform()
    {
        var dct = new DCTMatrix();
        var dctRef = new DctInt1Transform();

        byte[,] data =
        {
            {0, 0, 0, 0, 0, 0, 0, 0},
            {0, 150, 150, 0, 0, 0, 0, 0},
            {0, 150, 150, 0, 0, 0, 0, 0},
            {0, 150, 150, 0, 0, 0, 0, 0},
            {0, 90, 0, 0, 0, 0, 0, 0},
            {0, 90, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0},
            {0, 0, 0, 0, 0, 0, 0, 0},
        };

        var out1 = new int[8, 8];
        var out2 = new int[8, 8];
        
        dct.Forward(data, out1);
        dctRef.TransformForward(8, data, out2);
    }
}