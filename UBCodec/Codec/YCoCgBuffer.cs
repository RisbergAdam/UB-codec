using SkiaSharp;

namespace UBCodec.Codec;

public class YCoCgBuffer
{

    public byte[,] YBuffer;
    public byte[,] CoBuffer;
    public byte[,] CgBuffer;

    public int Width, Height;

    public static YCoCgBuffer FromSize(int blockSize)
    {
        var buffer = new YCoCgBuffer()
        {
            YBuffer = new byte[blockSize, blockSize],
            CoBuffer = new byte[blockSize/2, blockSize/2],
            CgBuffer = new byte[blockSize/2, blockSize/2],
            Width = blockSize,
            Height = blockSize,
        };
        return buffer;
    }

    public static YCoCgBuffer FromImage(SKBitmap image)
    {
        var buffer = new YCoCgBuffer()
        {
            YBuffer = new byte[image.Width, image.Height],
            CoBuffer = new byte[image.Width/2, image.Height/2],
            CgBuffer = new byte[image.Width/2, image.Height/2],
            Width = image.Width,
            Height = image.Height,
        };
        
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var color = image.GetPixel(x, y);
            var YCoCg = ToYCoCg(color);
            buffer.YBuffer[x, y] = YCoCg.Item1;
            buffer.CoBuffer[x/2, y/2] = YCoCg.Item2;
            buffer.CgBuffer[x/2, y/2] = YCoCg.Item3;
        }

        return buffer;
    }
    
    public static (byte, byte, byte) ToYCoCg(SKColor color)
    {
        byte r = color.Red;
        byte g = color.Green;
        byte b = color.Blue;

        byte Y = (byte) ((r >> 2) + (g >> 1) + (b >> 2));
        byte Co = (byte) ((r >> 1) - (b >> 1));
        byte Cg = (byte) ((g >> 1) - (r >> 2) - (b >> 2));
        
        return (Y, Co, Cg);
    }

    public byte GetY(int x, int y)
    {
        x = Math.Clamp(x, 0, Width-1);
        y = Math.Clamp(y, 0, Height-1);
        return YBuffer[x, y];
    }
    
    public byte GetCo(int x, int y)
    {
        x = Math.Clamp(x, 0, Width/2-1);
        y = Math.Clamp(y, 0, Height/2-1);
        return CoBuffer[x, y];
    }
    
    public byte GetCg(int x, int y)
    {
        x = Math.Clamp(x, 0, Width/2-1);
        y = Math.Clamp(y, 0, Height/2-1);
        return CgBuffer[x, y];
    }
}