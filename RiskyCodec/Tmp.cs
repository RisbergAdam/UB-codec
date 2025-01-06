namespace CodecTest;

public class Tmp
{
    /*public BitArray EncodePackage(BitArray frame, SKBitmap motionVecs)
    {
        var package = new BitArray(10000000);
        var bitsUsed = 0;

        for (var y = 0; y < motionVecs.Height; y++)
        for (var x = 0; x < motionVecs.Width; x++)
        {
            var p = motionVecs.GetPixel(x, y);
            var blockOffset = motionVecs.GetPixel(x, y);
            
            var xOffset = blockOffset.Red - 127 + 8;
            var yOffset = blockOffset.Green - 127 + 8;
            
            package.Set(bitsUsed++, (xOffset & (1 << 0)) != 0);
            package.Set(bitsUsed++, (xOffset & (1 << 1)) != 0);
            package.Set(bitsUsed++, (xOffset & (1 << 2)) != 0);
            package.Set(bitsUsed++, (xOffset & (1 << 3)) != 0);
            
            package.Set(bitsUsed++, (yOffset & (1 << 0)) != 0);
            package.Set(bitsUsed++, (yOffset & (1 << 1)) != 0);
            package.Set(bitsUsed++, (yOffset & (1 << 2)) != 0);
            package.Set(bitsUsed++, (yOffset & (1 << 3)) != 0);
        }

        package.Length = bitsUsed;
        return package;
    }*/

}