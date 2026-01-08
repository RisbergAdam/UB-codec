using UBCodec.Codec;
using UBCodec.Codec.NextGen;
using static UBCodec.Codec.ImageUtils;
using GolombRiceCoder = UBCodec.Codec.GolombRiceCoder;

Main(); return;

void Main()
{
    var codec = new UBCodec.Codec.UBCodec()
    {
        BlockSize = 16,
        MotionSearchDist = 5,
        Transformer = new UBCodec.Codec.DCTInteger1Transform(826),
        Coder = new GolombRiceCoder()
        {
            ZigZag = true,
            RLE = true,
            RLEMax = 65536,
            Golomb = true,
            GolombM = 128,
        }
    };

    var codec2 = new UBCodec2(new CodecConfig
    {
        BlockSize = 16,
        ReferenceBlockPadding = 4,
        MotionEstimator = new BlockMotionEstimatorReference(),
        DCT = new DCTInt1Transform(),
        Coder = new UBCodec.Codec.NextGen.GolombRiceCoder()
        {
            GolombM = 32
        },
    });

    var frame1 = BlockResize(ReadPng("../../../resources/mountain-village-split/frame_0001.png"), codec2.Config.BlockSize);
    var frame2 = BlockResize(ReadPng("../../../resources/mountain-village-split/frame_0004.png"), codec2.Config.BlockSize);
    
    // WritePng(YCoCgBuffer.FromBitmap(frame1).ToBitmap(), "../../../output/output.png");
    
    /*var encoded = codec2.EncodeFrame(frame1, frame2);
    var frame2rec = codec2.DecodeFrame(frame1, encoded);
    WritePng(frame1, "../../../output/input_f1.png");
    WritePng(frame2, "../../../output/input_f2.png");
    WritePng(frame2rec, "../../../output/input_f2_rec.png");*/
    
    var prevFrame = BlockResize(ReadPng("../../../resources/mountain-village-split/frame_0001.png"), codec2.Config.BlockSize);

    for (var i = 2; i < 100; i++)
    {
        Console.WriteLine($"Encoding frame {i}...");
        var frameFile = $"frame_{i.ToString().PadLeft(4, '0')}.png";
        
        var currFrame = BlockResize(ReadPng($"../../../resources/mountain-village-split/{frameFile}"), codec2.Config.BlockSize);
        var encoded = codec2.EncodeFrame(prevFrame, currFrame);
        var frameRec = codec2.DecodeFrame(prevFrame, encoded);

        WritePng(frameRec, "../../../output/rec_" + frameFile);
        
        prevFrame = currFrame;
    }

    Console.WriteLine("Done!");
}