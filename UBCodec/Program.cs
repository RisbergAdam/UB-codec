using UBCodec.Codec;
using UBCodec.Codec.NextGen;
using static UBCodec.Codec.ImageUtils;

Main(); return;

void Main()
{
    var core = new EncoderCore();
    
    var frame1 = BlockResize(ReadPng("../../../resources/ezgif-6-e3be30c4ce-png-split/ezgif-frame-001.png"), 32);
    var frame2 = BlockResize(ReadPng("../../../resources/ezgif-6-e3be30c4ce-png-split/ezgif-frame-020.png"), 32);
    var frame2half = Downsample(frame2);
    
    for (var y = 0; y < 8; y++)
    for (var x = 0; x < 8; x++)
    {
        core.LoadBlock(frame1, frame2, frame2half, x, y);
        core.ReadBlock(frame2, x, y);
    }
    
    ImageUtils.WritePng(frame2, "../../../output/output_f2.png");
    
    Console.WriteLine("Done!");
}