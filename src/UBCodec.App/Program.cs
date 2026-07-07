using UBCodec.Core.Encoder;
using UBCodec.Core.Utils;
using static UBCodec.Core.Utils.ImageUtils;

Main(); return;

void Main()
{
    var config = new CodecConfig
    {
        BlockSize = 16,
        ReferenceBlockPadding = 4,
        MotionEstimator = new NoopMotionEstimator(),
        DCT = new DctInt1Transform(),
        Coder = new GolombRiceCoder
        {
            GolombM = 32
        },
    };
    
    var encoder = new SoftwareEncoder(config);
    
    string GetFramePath(int index, int padding = 0)
    {
        const string framePattern = "../../../resources/input_cars/frame_{}.png";
        return framePattern.Replace("{}", index.ToString().PadLeft(padding, '0'));
    }
    
    var accFrame = BlockResize(ReadPng(GetFramePath(1)), config.BlockSize);
    var accBuffer = YCoCgBuffer.FromBitmap(accFrame);

    for (var i = 2; i < 2; i++)
    {
        TimeExec("iteration", () =>
        {
            Console.WriteLine($"Encoding frame {i}...");
            var currFrame = TimeExec("Read frame", () =>
            {
                return BlockResize(ReadPng(GetFramePath(i)), config.BlockSize);
            });

            var currBuffer = YCoCgBuffer.FromBitmap(currFrame);
            var encoded = TimeExec("encode", () => encoder.EncodeFrame(accBuffer, currBuffer, i));
            var frameRecBuffer = YCoCgBuffer.FromSize(currBuffer.Width, currBuffer.Height);
            TimeExec("decode", () =>
            {
                encoder.DecodeFrame(accBuffer, frameRecBuffer, encoded);
                return 0;
            });

            TimeExec("write", () =>
            {
                WritePng(frameRecBuffer.ToBitmap(), "../../../artifacts/rec_" + i + ".png");
                return 0;
            });
            
            accBuffer = frameRecBuffer;
            return 0;
        });
    }

    Console.WriteLine("Done!");
}