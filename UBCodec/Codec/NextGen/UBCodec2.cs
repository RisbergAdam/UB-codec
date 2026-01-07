using System.Collections;
using System.Diagnostics;
using System.Drawing;
using SkiaSharp;

namespace UBCodec.Codec.NextGen;

public class CodecConfig
{
    public int BlockSize { get; set; }
    
    public int ReferenceBlockPadding { get; set; }

    public IBlockMotionEstimator MotionEstimator { get; set; }
    
    public ITransform DCT { get; set; }
    
    public ICoder Coder { get; set; }
}

public class UBCodec2(CodecConfig config)
{

    public CodecConfig Config => config;
    
    private EncoderCore _core = new(config);
    
    public byte[] EncodeFrame(SKBitmap prevBitmap, SKBitmap currBitmap)
    {
        var sw = new Stopwatch();
        sw.Start();
        var byteStream = new ByteStreamWriter();
        
        var prev = YCoCgBuffer.FromImage(prevBitmap);
        var curr = YCoCgBuffer.FromImage(currBitmap);
        
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
            _core.Encode(byteStream);
        }

        sw.Stop();
        var bytes = byteStream.GetArray();
        Console.WriteLine($"Encoded {xBlocks*yBlocks} blocks in {sw.ElapsedMilliseconds}ms into {bytes.Length / 1024} kb");
        return bytes;
    }

    public SKBitmap DecodeFrame(SKBitmap prevBitmap, byte[] encoded)
    {
        throw new NotImplementedException();
    }
}