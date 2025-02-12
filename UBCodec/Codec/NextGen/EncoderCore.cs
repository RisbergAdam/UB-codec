using SkiaSharp;

namespace UBCodec.Codec.NextGen;

public class EncoderCore
{
    private BlockMemory ReferenceMem = new (400);
    
    const int BLOCK_U_OFFSET = 256;
    const int BLOCK_V_OFFSET = 256 + 64;
    
    private BlockMemory BlockMem = new (256 + 64 + 64);

    public void LoadBlock(SKBitmap prev, SKBitmap curr, SKBitmap currHalf, int xb, int yb)
    {
        // load curr into block mem
        
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x += 4)
        {
            var c1 = Utils.ToYUV(curr.GetPixel(xb*32 + x + 0, yb*32 + y));
            var c2 = Utils.ToYUV(curr.GetPixel(xb*32 + x + 1, yb*32 + y));
            var c3 = Utils.ToYUV(curr.GetPixel(xb*32 + x + 2, yb*32 + y));
            var c4 = Utils.ToYUV(curr.GetPixel(xb*32 + x + 3, yb*32 + y));

            if (xb*32 + x + 0 == 0 && yb*32 + y == 0)
            {
                Console.WriteLine($"{c1.Red} {c2.Red} {c3.Red} {c4.Red}");
            }
            
            BlockMem.Write(x/4 + y * 32/4, Utils.Pack10BitValues([
                c1.Red,
                c2.Red,
                c3.Red,
                c4.Red,
            ]));
        }
        
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x += 4)
        {
            var c1 = Utils.ToYUV(currHalf.GetPixel(xb*16 + x + 0, yb*16 + y));
            var c2 = Utils.ToYUV(currHalf.GetPixel(xb*16 + x + 1, yb*16 + y));
            var c3 = Utils.ToYUV(currHalf.GetPixel(xb*16 + x + 2, yb*16 + y));
            var c4 = Utils.ToYUV(currHalf.GetPixel(xb*16 + x + 3, yb*16 + y));
            
            BlockMem.Write(BLOCK_U_OFFSET + x/4 + y * 16/4, Utils.Pack10BitValues([
                c1.Green,
                c2.Green,
                c3.Green,
                c4.Green,
            ]));
            
            BlockMem.Write(BLOCK_V_OFFSET + x/4 + y * 16/4, Utils.Pack10BitValues([
                c1.Blue,
                c2.Blue,
                c3.Blue,
                c4.Blue,
            ]));
        }
        
        // load prev into ref mem
        
        for (int y = 0; y < 40; y++)
        for (int x = 0; x < 40; x += 4)
        {
            var c1 = Utils.ToYUV(prev.GetPixelSafe(xb*32 + x + 0 - 4, yb*32 + y - 4));
            var c2 = Utils.ToYUV(prev.GetPixelSafe(xb*32 + x + 1 - 4, yb*32 + y - 4));
            var c3 = Utils.ToYUV(prev.GetPixelSafe(xb*32 + x + 2 - 4, yb*32 + y - 4));
            var c4 = Utils.ToYUV(prev.GetPixelSafe(xb*32 + x + 3 - 4, yb*32 + y - 4));
            ReferenceMem.Write(x / 4 + y * 40/4, Utils.Pack8BitValues([
                c1.Red,
                c2.Red,
                c3.Red,
                c4.Red
            ]));
        }
    }

    public void ReadBlock(SKBitmap target, int xb, int yb)
    {
        for (var y = 0; y < 32; y++)
        for (var x = 0; x < 32; x += 4)
        {
            var yRow = Utils.Unpack10BitValues(BlockMem.Read(x/4 + y * 32/4));
            var uRow = Utils.Unpack10BitValues(BlockMem.Read(BLOCK_U_OFFSET + x/2/4 + y/2 * 16/4));
            var vRow = Utils.Unpack10BitValues(BlockMem.Read(BLOCK_V_OFFSET + x/2/4 + y/2 * 16/4));

            for (var i = 0; i < 4; i++)
            {
                var Y = (byte)yRow[i];
                var u = (byte)uRow[(x + i) / 2 % 4];
                var v = (byte)vRow[(x + i) / 2 % 4];
                
                var c = Utils.FromYUV(new SKColor(Y, u, v));
                target.SetPixel(xb * 32 + x + i, yb * 32 + y, c);
            }
        }
    }
    


}