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
    
    public byte[] EncodeFrame(SKBitmap prevBitmap, SKBitmap currBitmap, int frameSeq)
    {
        var byteStream = new ByteStreamWriter();
        byteStream.WriteUInt16((ushort) frameSeq);
        
        var prev = YCoCgBuffer.FromBitmap(prevBitmap);
        var curr = YCoCgBuffer.FromBitmap(currBitmap);
        
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
            _core.Encode(byteStream, frameSeq);
        }
        
        var bytes = byteStream.GetArray();
        Console.WriteLine($"Encoded {xBlocks*yBlocks} blocks into {bytes.Length / 1024} kb");
        return bytes;
    }

    public SKBitmap DecodeFrame(SKBitmap prevBitmap, byte[] encoded)
    {
        var prev = YCoCgBuffer.FromBitmap(prevBitmap);
        var curr = YCoCgBuffer.FromSize(prev.Width, prev.Height);

        var byteStream = new ByteStreamReader(encoded);
        var frameSeq = (int) byteStream.ReadUInt16();
        
        var xBlocks = curr.Width / config.BlockSize;
        var yBlocks = curr.Height / config.BlockSize;
        for (var yBlock = 0; yBlock < yBlocks; yBlock++)
        for (var xBlock = 0; xBlock < xBlocks; xBlock++)
        {
            _core.Decode(byteStream, prev, curr, frameSeq);   
        }

        var bitmap = curr.ToBitmap();
        return bitmap;
    }
}