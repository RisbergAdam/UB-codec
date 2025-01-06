using System.Diagnostics;
using SkiaSharp;

namespace RiskyCodec.Codec;

public class ImageUtils
{
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