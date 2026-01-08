using SkiaSharp;

namespace UBCodec.Codec;

public class YCoCgBuffer
{
    private const int D = 2; // chroma downsample count
    
    public byte[,] YBuffer;
    public byte[,] CoBuffer;
    public byte[,] CgBuffer;

    public int Width, Height;

    public static YCoCgBuffer FromSize(int width, int height)
    {
        var chromaWidth = width / D;
        var chromaHeight = height / D;
        var buffer = new YCoCgBuffer
        {
            YBuffer = new byte[width, height],
            CoBuffer = new byte[chromaWidth, chromaHeight],
            CgBuffer = new byte[chromaWidth, chromaHeight],
            Width = width,
            Height = height,
        };
        return buffer;
    }

    public static YCoCgBuffer FromBitmap(SKBitmap bitmap)
    {
        var chromaWidth = bitmap.Width / D;
        var chromaHeight = bitmap.Height / D;
        var buffer = new YCoCgBuffer
        {
            YBuffer = new byte[bitmap.Width, bitmap.Height],
            CoBuffer = new byte[chromaWidth, chromaHeight],
            CgBuffer = new byte[chromaWidth, chromaHeight],
            Width = bitmap.Width,
            Height = bitmap.Height,
        };
        
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var color = bitmap.GetPixel(x, y);
            var YCoCg = ToYCoCg(color);
            buffer.YBuffer[x, y] = YCoCg.Item1;
            buffer.CoBuffer[x/D, y/D] += (byte) (YCoCg.Item2/D/D);
            buffer.CgBuffer[x/D, y/D] += (byte) (YCoCg.Item3/D/D);
        }

        return buffer;
    }

    public SKBitmap ToBitmap()
    {
        var image = new SKBitmap(Width, Height, SKColorType.Rgb888x, SKAlphaType.Opaque);

        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var YCoCg = (
                YBuffer[x, y],
                CoBuffer[x/D, y/D],
                CgBuffer[x/D, y/D]
            );
            
            image.SetPixel(x, y, FromYCoCg(YCoCg));
        }
        
        return image;
    }
    
    public static (byte, byte, byte) ToYCoCg(SKColor color)
    {
        byte r = color.Red;
        byte g = color.Green;
        byte b = color.Blue;

        byte Y = (byte) (((r+b) >> 2) + (g >> 1));
        byte Co = (byte) (((r-b) >> 1) + 127);
        byte Cg = (byte) ((g >> 1) - ((r+b) >> 2) + 127);
        
        return (Y, Co, Cg);
    }
    
    public static SKColor FromYCoCg((byte, byte, byte) YCoCg)
    {
        var (Y, Co, Cg) = YCoCg;
        byte r = (byte) Math.Clamp(Y + Co - Cg, 0, 255);
        byte g = (byte) Math.Clamp(Y + (Cg - 127), 0, 255);
        byte b = (byte) Math.Clamp(Y - Co - Cg + 254, 0, 255);
        return new SKColor(r, g, b);
    }

    public byte GetY(int x, int y)
    {
        x = Math.Clamp(x, 0, Width-1);
        y = Math.Clamp(y, 0, Height-1);
        return YBuffer[x, y];
    }
    
    public byte GetCo(int x, int y)
    {
        x = Math.Clamp(x, 0, Width/D-1);
        y = Math.Clamp(y, 0, Height/D-1);
        return CoBuffer[x, y];
    }
    
    public byte GetCg(int x, int y)
    {
        x = Math.Clamp(x, 0, Width/D-1);
        y = Math.Clamp(y, 0, Height/D-1);
        return CgBuffer[x, y];
    }
}