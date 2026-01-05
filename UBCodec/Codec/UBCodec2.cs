using System.Collections;
using System.Drawing;
using SkiaSharp;

namespace UBCodec.Codec;

public class CodecConfig
{
    public int BlockSize { get; set; }
    
    public int MotionSearchDist { get; set; }
    
    public ITransform DCT { get; set; }
    
    public ICoder Coder { get; set; }
}

public class UBCodec2(CodecConfig config)
{

    public CodecConfig Config => config;
    
    private CodecCore _core = new(config);
    
    public BitArray EncodeFrame(YCoCgBuffer prev, YCoCgBuffer curr)
    {
        var xBlocks = curr.Width / config.BlockSize;
        var yBlocks = curr.Height / config.BlockSize;
        for (var yBlock = 0; yBlock < yBlocks; yBlock++)
        for (var xBlock = 0; xBlock < xBlocks; xBlock++)
        {
            var region = new Rectangle(
                xBlock * config.BlockSize,
                yBlock * config.BlockSize, 
                config.BlockSize,
                config.BlockSize);
            _core.LoadBlock(prev, curr, region);
            _core.Encode();
        }

        throw new NotImplementedException();
    }

    public SKBitmap DecodeFrame(SKBitmap prev, BitArray encoded)
    {
        throw new NotImplementedException();
    }
}