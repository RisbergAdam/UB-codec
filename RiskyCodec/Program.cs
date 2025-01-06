using System.Diagnostics;
using RiskyCodec.Codec;
using SkiaSharp;

Main(); return;

void Main()
{
    var codec = new RiskyCodec.Codec.RiskyCodec()
    {
        BlockSize = 16,
        MotionSearchDist = 5,
        Transformer = new DCTInteger1Transform(826),
        Coder = new GolombRiceCoder()
        {
            ZigZag = true,
            RLE = true,
            RLEMax = 65536,
            Golomb = true,
            GolombM = 64,
        }
    };
    
    var frame1 = BlockResize(ReadPng("../../../resources/ezgif-6-e3be30c4ce-png-split/ezgif-frame-001.png"), codec.BlockSize);
    var frame2 = BlockResize(ReadPng("../../../resources/ezgif-6-e3be30c4ce-png-split/ezgif-frame-020.png"), codec.BlockSize);
    
    var (encoded, motionVecs) = codec.EncodeFrame(frame1, frame2);
    var frame2rec = codec.ReconstructFrame(frame1, encoded, motionVecs);
    
    WritePng(frame1, "../../../output/input_f1.png");
    WritePng(frame2, "../../../output/input_f2.png");
    WritePng(frame2rec, "../../../output/input_f2_rec.png");
    
    Console.WriteLine("Done!");
}

SKBitmap BlockResize(SKBitmap input, int blockSize)
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

SKBitmap ReadPng(string path)
{
    using var stream = new SKFileStream(path);
    var codec = SKCodec.Create(stream);
    var bitmap = new SKBitmap(codec.Info);
    codec.GetPixels(bitmap.Info, bitmap.GetPixels());
    return bitmap;
}

void WritePng(SKBitmap input, string path)
{
    using var data = input.Encode(SKEncodedImageFormat.Png, 80);
    using var stream = File.OpenWrite(path);
    data.SaveTo(stream);
}

T TimeExec<T>(string name, Func<T> action)
{
    Stopwatch s = new Stopwatch();
    s.Start();
    var r = action();
    s.Stop();
    Console.WriteLine($"TimeExec[{name}]: {s.Elapsed.TotalMilliseconds}ms");
    return r;
}