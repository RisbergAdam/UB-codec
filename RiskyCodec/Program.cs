using RiskyCodec.Codec;

using static RiskyCodec.Codec.ImageUtils;

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