using System.Diagnostics;
using SkiaSharp;

namespace UBCodec.Codec;

public static class ImageUtils
{
    public static SKColor GetPixelSafe(this SKBitmap img, int x, int y)
    {
        if (x < 0 || y < 0) return new SKColor(0, 0, 0);
        if (x >= img.Width || y >= img.Height) return new SKColor(0, 0, 0);
        return img.GetPixel(x, y);
    }
    
    public static SKBitmap BlockResize(SKBitmap input, int blockSize)
    {
        var w = input.Width / blockSize * blockSize;
        var h = input.Height / blockSize * blockSize;
        var output = new SKBitmap(w, h, SKColorType.Rgb888x, SKAlphaType.Opaque);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                output.SetPixel(x, y, input.GetPixel(x, y));
            }
        }

        return output;
    }

    public static SKBitmap ReadPng(string path)
    {
        using var stream = new SKFileStream(path);
        var codec = SKCodec.Create(stream);
        var bitmap = new SKBitmap(codec.Info);
        codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        return bitmap;
    }

    public static void WritePng(SKBitmap input, string path)
    {
        using var data = input.Encode(SKEncodedImageFormat.Png, 80);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }

    public static SKBitmap Downsample(SKBitmap input)
    {
        var output = new SKBitmap(input.Width/2, input.Height/2);
        for (var y = 0; y < output.Height; y++)
        for (var x = 0; x < output.Width; x++)
        {
            int r = 0, g = 0, b = 0;
            for (var y2 = 0; y2 < 2; y2++)
            for (var x2 = 0; x2 < 2; x2++)
            {
                var c = input.GetPixel(x * 2 + x2, y * 2 + y2);
                r += c.Red;
                g += c.Green;
                b += c.Blue;
            }
            r /= 4;
            g /= 4;
            b /= 4;
            output.SetPixel(x, y, new SKColor((byte)r, (byte)g, (byte)b));
        }

        return output;
    }

    public static T TimeExec<T>(string name, Func<T> action)
    {
        Stopwatch s = new Stopwatch();
        s.Start();
        var r = action();
        s.Stop();
        Console.WriteLine($"TimeExec[{name}]: {s.Elapsed.TotalMilliseconds}ms");
        return r;
    }
}