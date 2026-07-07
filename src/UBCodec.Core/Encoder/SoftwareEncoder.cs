using System.Diagnostics;
using System.Drawing;
using UBCodec.Core.Encoder;

namespace UBCodec.Core.Encoder;

public class SoftwareEncoder(CodecConfig config)
{
    public CodecConfig Config => config;
    
    private EncoderCore _core = new(config);
    
    public byte[] EncodeFrame(YCoCgBuffer prev, YCoCgBuffer curr, int frameSeq)
    {
        var byteStream = new ByteStreamWriter();
        byteStream.WriteUInt16((ushort) frameSeq);
        
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

    public void DecodeFrame(YCoCgBuffer prev, YCoCgBuffer curr, byte[] encoded)
    {
        var byteStream = new ByteStreamReader(encoded);
        var frameSeq = (int) byteStream.ReadUInt16();
        
        var xBlocks = curr.Width / config.BlockSize;
        var yBlocks = curr.Height / config.BlockSize;
        for (var yBlock = 0; yBlock < yBlocks; yBlock++)
        for (var xBlock = 0; xBlock < xBlocks; xBlock++)
        {
            _core.Decode(byteStream, prev, curr, frameSeq);   
        }
    }
}