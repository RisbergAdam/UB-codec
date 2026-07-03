using System.Diagnostics;
using SkiaSharp;
using UBCodec.Codec;
using UBCodec.Codec.NextGen;
using static UBCodec.Codec.ImageUtils;
using GolombRiceCoder = UBCodec.Codec.GolombRiceCoder;

Main(); return;

void Main()
{
    var codec2 = new UBCodec2(new CodecConfig
    {
        BlockSize = 16,
        ReferenceBlockPadding = 4,
        MotionEstimator = new NoopMotionEstimator(),
        // MotionEstimator = new BlockMotionEstimatorReference(),
        DCT = new DCTInt1Transform(),
        Coder = new UBCodec.Codec.NextGen.GolombRiceCoder()
        {
            GolombM = 32
        },
    });
    
    string GetFramePath(int index, int padding = 0)
    {
        const string framePattern = "../../../input_cars/frame_{}.png";
        return framePattern.Replace("{}", index.ToString().PadLeft(padding, '0'));
    }
    
    var accFrame = BlockResize(ReadPng(GetFramePath(1)), codec2.Config.BlockSize);

    for (var i = 2; i < 100; i++)
    {
        TimeExec("iteration", () =>
        {
            Console.WriteLine($"Encoding frame {i}...");
            var currFrame = TimeExec("Read frame", () =>
            {
                return BlockResize(ReadPng(GetFramePath(i)), codec2.Config.BlockSize);
            });

            var encoded = TimeExec("encode", () => codec2.EncodeFrame(accFrame, currFrame, i));
            var frameRec = TimeExec("decode", () => codec2.DecodeFrame(accFrame, encoded));

            TimeExec("write", () =>
            {
                WritePng(frameRec, "../../../output/rec_" + i + ".png");
                return 0;
            });
            
            accFrame = frameRec;
            return 0;
        });
    }

    Console.WriteLine("Done!");
}