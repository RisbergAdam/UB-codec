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

    var frame1 = BlockResize(ReadPng("../../../resources/ezgif-6-e3be30c4ce-png-split/ezgif-frame-001.png"), codec2.Config.BlockSize);
    var frame2 = BlockResize(ReadPng("../../../resources/ezgif-6-e3be30c4ce-png-split/ezgif-frame-010.png"), codec2.Config.BlockSize);

    /*var (encoded, motionVecs) = codec.EncodeFrame(frame1, frame2);
    var frame2rec = codec.ReconstructFrame(frame1, encoded, motionVecs);
    
    WritePng(frame1, "../../../output/input_f1.png");
    WritePng(frame2, "../../../output/input_f2.png");
    WritePng(frame2rec, "../../../output/input_f2_rec.png");*/

    // codec.EncodeFrame(frame1, frame2);
    var encoded = codec2.EncodeFrame(frame1, frame2);
    var frame2rec = codec2.DecodeFrame(frame1, encoded);

    /*var tr = new UBCodec.Codec.DCTReferenceTransform();
    var t1 = new UBCodec.Codec.DCTInteger1Transform(826);
    var t2 = new DCTInt1Transform();

    int[,] data =
    {
        { 150, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 0, 0, 0 },
    };

    Console.WriteLine("reference:"); PrintArray(tr.Transform(data, false));
    Console.WriteLine("integer1:"); PrintArray(t1.Transform(data, false));
    Console.WriteLine("int2:"); PrintArray(t2.Transform(data, false));

    var dctMat = DCTInt1Transform.CreateMatrix(32);
    Console.WriteLine("dctMat:"); PrintArray(dctMat);*/

    Console.WriteLine("Done!");
}

void PrintArray(int[,] array)
{
    for (int row = 0; row < array.GetLength(0); row++)
    {
        Console.Write("{");
        for (int col = 0; col < array.GetLength(1); col++)
        {
            Console.Write(array[row, col]);
            if (col < array.GetLength(1) - 1) 
                Console.Write(", ");
        }
        Console.WriteLine("},");
    }
}