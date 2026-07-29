using SkiaSharp;
using UBCodec.Core.Encoder.ColorSpace;

namespace UBCodec.Core.Encoder;

public class PlanarImage
{
    public byte[,] LBuffer; // Luma buffer
    public byte[,] C1Buffer; // Chroma-1 buffer
    public byte[,] C2Buffer; // Chroma-2 buffer

    public int Width, Height;

    public int ChromaDownsample = 2;

    private static IColorSpace _CP = new YCbCrColorSpace();

    public static PlanarImage FromSize(int width, int height, int D = 1)
    {
        var chromaWidth = width / D;
        var chromaHeight = height / D;
        var buffer = new PlanarImage
        {
            LBuffer = new byte[width, height],
            C1Buffer = new byte[chromaWidth, chromaHeight],
            C2Buffer = new byte[chromaWidth, chromaHeight],
            Width = width,
            Height = height,
            ChromaDownsample = D,
        };
        return buffer;
    }

    public static PlanarImage FromBitmap(SKBitmap bitmap, int D = 1)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var chromaWidth = width / D;
        var chromaHeight = height / D;
        var buffer = new PlanarImage
        {
            LBuffer = new byte[width, height],
            C1Buffer = new byte[chromaWidth, chromaHeight],
            C2Buffer = new byte[chromaWidth, chromaHeight],
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

            // Luma: full resolution
            for (var y = 0; y < height; y++)
            {
                var row = basePtr + y * rowBytes;
                for (var x = 0; x < width; x++)
                {
                    var px = row + x * bytesPerPixel;
                    var (Y, _, _) = _CP.ToSpace(px[rOff], px[gOff], px[bOff]);
                    buffer.LBuffer[x, y] = Y;
                }
            }

            // Chroma: subsampled, average each D×D group
            var div = D * D;
            var half = div / 2;
            for (var cy = 0; cy < chromaHeight; cy++)
            for (var cx = 0; cx < chromaWidth; cx++)
            {
                var coSum = 0;
                var cgSum = 0;
                for (var dy = 0; dy < D; dy++)
                for (var dx = 0; dx < D; dx++)
                {
                    var x = cx * D + dx;
                    var y = cy * D + dy;
                    var px = basePtr + y * rowBytes + x * bytesPerPixel;
                    var (_, Co, Cg) = _CP.ToSpace(px[rOff], px[gOff], px[bOff]);
                    coSum += Co;
                    cgSum += Cg;
                }
                buffer.C1Buffer[cx, cy] = (byte)((coSum + half) / div);
                buffer.C2Buffer[cx, cy] = (byte)((cgSum + half) / div);
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
                    byte l = LBuffer[x, y];
                    byte c1 = C1Buffer[x / ChromaDownsample, y / ChromaDownsample];
                    byte c2 = C2Buffer[x / ChromaDownsample, y / ChromaDownsample];

                    var (r, g, b) = _CP.FromSpace(l, c1, c2);

                    var px = row + x * bytesPerPixel;
                    px[0] = r; px[1] = g; px[2] = b;
                    if (bytesPerPixel == 4) px[3] = 0xFF;
                }
            }
        }

        return image;
    }
    
    public byte LetL(int x, int y)
    {
        x = Math.Clamp(x, 0, Width-1);
        y = Math.Clamp(y, 0, Height-1);
        return LBuffer[x, y];
    }
    
    public byte GetC1(int x, int y)
    {
        x = Math.Clamp(x, 0, Width/ChromaDownsample-1);
        y = Math.Clamp(y, 0, Height/ChromaDownsample-1);
        return C1Buffer[x, y];
    }
    
    public byte GetC2(int x, int y)
    {
        x = Math.Clamp(x, 0, Width/ChromaDownsample-1);
        y = Math.Clamp(y, 0, Height/ChromaDownsample-1);
        return C2Buffer[x, y];
    }
}