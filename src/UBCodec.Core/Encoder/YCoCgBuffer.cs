using SkiaSharp;

namespace UBCodec.Core.Encoder;

public class YCoCgBuffer
{
    public byte[,] YBuffer;
    public byte[,] CoBuffer;
    public byte[,] CgBuffer;

    public int Width, Height;

    public int ChromaDownsample = 2;

    public static YCoCgBuffer FromSize(int width, int height, int D)
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
            ChromaDownsample = D,
        };
        return buffer;
    }

    public static YCoCgBuffer FromBitmap(SKBitmap bitmap, int D)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var chromaWidth = width / D;
        var chromaHeight = height / D;
        var buffer = new YCoCgBuffer
        {
            YBuffer = new byte[width, height],
            CoBuffer = new byte[chromaWidth, chromaHeight],
            CgBuffer = new byte[chromaWidth, chromaHeight],
            Width = width,
            Height = height,
            ChromaDownsample = D,
        };

        var bytesPerPixel = bitmap.BytesPerPixel;
        var rowBytes = bitmap.RowBytes;
        var colorType = bitmap.ColorType;

        int rOff, gOff, bOff;
        switch (colorType)
        {
            case SKColorType.Bgra8888:
                bOff = 0; gOff = 1; rOff = 2;
                break;
            case SKColorType.Rgba8888:
            case SKColorType.Rgb888x:
            default:
                rOff = 0; gOff = 1; bOff = 2;
                break;
        }

        unsafe
        {
            var basePtr = (byte*)bitmap.GetPixels().ToPointer();
            for (var y = 0; y < height; y++)
            {
                var row = basePtr + y * rowBytes;
                for (var x = 0; x < width; x++)
                {
                    var px = row + x * bytesPerPixel;
                    var (Y, Co, Cg) = ToYCoCg(px[rOff], px[gOff], px[bOff]);

                    buffer.YBuffer[x, y] = Y;
                    buffer.CoBuffer[x / D, y / D] += (byte)(Co / D / D);
                    buffer.CgBuffer[x / D, y / D] += (byte)(Cg / D / D);
                }
            }
        }

        return buffer;
    }

    public SKBitmap ToBitmap()
    {
        var image = new SKBitmap(Width, Height, SKColorType.Rgb888x, SKAlphaType.Opaque);
        var bytesPerPixel = image.BytesPerPixel;
        var rowBytes = image.RowBytes;

        unsafe
        {
            var basePtr = (byte*)image.GetPixels().ToPointer();
            for (var y = 0; y < Height; y++)
            {
                var row = basePtr + y * rowBytes;
                for (var x = 0; x < Width; x++)
                {
                    byte Y = YBuffer[x, y];
                    byte Co = CoBuffer[x / ChromaDownsample, y / ChromaDownsample];
                    byte Cg = CgBuffer[x / ChromaDownsample, y / ChromaDownsample];

                    var (r, g, b) = FromYCoCg(Y, Co, Cg);

                    var px = row + x * bytesPerPixel;
                    px[0] = r; px[1] = g; px[2] = b;
                    if (bytesPerPixel == 4) px[3] = 0xFF;
                }
            }
        }

        return image;
    }
    
    public static (byte, byte, byte) ToYCoCg(byte r, byte g, byte b)
    {
        byte Y = (byte) (((r+b) >> 2) + (g >> 1));
        byte Co = (byte) (((r-b) >> 1) + 127);
        byte Cg = (byte) ((g >> 1) - ((r+b) >> 2) + 127);
        
        return (Y, Co, Cg);
    }
    
    public static (byte, byte, byte) FromYCoCg(byte Y, byte Co, byte Cg)
    {
        byte r = (byte) Math.Clamp(Y + Co - Cg, 0, 255);
        byte g = (byte) Math.Clamp(Y + (Cg - 127), 0, 255);
        byte b = (byte) Math.Clamp(Y - Co - Cg + 254, 0, 255);
        return (r, g, b);
    }

    public byte GetY(int x, int y)
    {
        x = Math.Clamp(x, 0, Width-1);
        y = Math.Clamp(y, 0, Height-1);
        return YBuffer[x, y];
    }
    
    public byte GetCo(int x, int y)
    {
        x = Math.Clamp(x, 0, Width/ChromaDownsample-1);
        y = Math.Clamp(y, 0, Height/ChromaDownsample-1);
        return CoBuffer[x, y];
    }
    
    public byte GetCg(int x, int y)
    {
        x = Math.Clamp(x, 0, Width/ChromaDownsample-1);
        y = Math.Clamp(y, 0, Height/ChromaDownsample-1);
        return CgBuffer[x, y];
    }
}