using System.Collections;
using System.Diagnostics;
using System.Drawing;
using SkiaSharp;

namespace UBCodec.Codec.NextGen;

class EncoderCore(CodecConfig config)
{
    private byte[,] _YBufferPrev;
    private byte[,] _CoBufferPrev;
    private byte[,] _CgBufferPrev;
    
    private byte[,] _YBuffer;
    private byte[,] _CoBuffer;
    private byte[,] _CgBuffer;
    
    private byte[,] _workmem1;
    private int[,] _workmem2;
    
    private Rectangle _region;

    public void LoadBlock(YCoCgBuffer prev, YCoCgBuffer curr, Rectangle region)
    {
        _region = region;

        var searchWindowSize = config.BlockSize + config.ReferenceBlockPadding * 2;
        var blockSize = config.BlockSize;
        
        _YBufferPrev = new byte[searchWindowSize, searchWindowSize];
        _CoBufferPrev = new byte[searchWindowSize/2, searchWindowSize/2];
        _CgBufferPrev = new byte[searchWindowSize/2, searchWindowSize/2];
        
        _YBuffer = new byte[blockSize, blockSize];
        _CgBuffer = new byte[blockSize/2, blockSize/2];
        _CoBuffer = new byte[blockSize/2, blockSize/2];
        
        _workmem1 =  new byte[blockSize, blockSize];
        _workmem2 =  new int[blockSize, blockSize];
        
        for (var y = 0; y < blockSize; y++)
        for (var x = 0; x < blockSize; x++)
        {
            var sx = region.X + x;
            var sy = region.Y + y;
            _YBuffer[x, y] = curr.GetY(sx, sy);
            _CoBuffer[x/2, y/2] = curr.GetCo(sx/2, sy/2);
            _CgBuffer[x/2, y/2] = curr.GetCg(sx/2, sy/2);
        }
        
        for (var y = 0; y < searchWindowSize; y++)
        for (var x = 0; x < searchWindowSize; x++)
        {
            var sx = region.X - config.ReferenceBlockPadding + x;
            var sy = region.Y - config.ReferenceBlockPadding + y;
            _YBufferPrev[x, y] = prev.GetY(sx, sy);
            _CoBufferPrev[x/2, y/2] = prev.GetCo(sx/2, sy/2);
            _CgBufferPrev[x/2, y/2] = prev.GetCg(sx/2, sy/2);
        }
    }
    
    public void Encode(ByteStreamWriter byteStream)
    {
        var streamSize = byteStream.Count;
        var endSpan = MeasureTime();
        var blockMotion = config.MotionEstimator.EstimateMotion(_YBuffer, _YBufferPrev);
        endSpan("blockMotion");
        ComputeResidual(_YBuffer, _YBufferPrev, blockMotion, output: _workmem1);
        endSpan("residual");
        config.DCT.Transform(config.BlockSize, _workmem1, false, output: _workmem2);
        endSpan("transform");
        QuantizeCoefficients(config.BlockSize, _workmem2);
        endSpan("quantize");
        WriteBlockHeader(byteStream, blockMotion);
        config.Coder.Encode(config.BlockSize, _workmem2, output: byteStream);
        endSpan("encode");
        var blockBytes = byteStream.Count - streamSize;
    }

    private Action<string> MeasureTime()
    {
        var sw = new Stopwatch();
        sw.Start();
        
        return (spanName) =>
        {
            // Console.WriteLine($"Span {spanName} time: {sw.ElapsedTicks} ticks");
            sw.Restart();
        };
    }

    private void WriteBlockHeader(ByteStreamWriter stream, MotionEstimate blockMotion)
    {
        stream
            .WriteUInt16((ushort)_region.X)
            .WriteUInt16((ushort)_region.Y)
            .WriteUInt16((ushort)_region.Width)
            .WriteUInt16((ushort)_region.Height)
            .WriteUInt8((byte)(blockMotion.X + 127))
            .WriteUInt8((byte)(blockMotion.Y + 127));
    }

    private SKColor FromYCoCg((byte, byte, byte) YCoCg)
    {
        var (Y, Co, Cg) = YCoCg;
        byte r = (byte) (Y + Co - Cg);
        byte g = (byte) (Y + Cg);
        byte b = (byte) (Y - Cg - Cg);
        return new SKColor(r, g, b);
    }

    private void ComputeResidual(byte[,] buffer, byte[,] prevBuffer, MotionEstimate blockMotion, byte[,] output)
    {
        var bufferSize = buffer.GetLength(0);
        var dx = blockMotion.X;
        var dy = blockMotion.Y;
        
        for (var y = 0; y < bufferSize; y++)
        for (var x = 0; x < bufferSize; x++)
        {
            output[x, y] = (byte) ((buffer[x, y] - prevBuffer[x + dx + config.ReferenceBlockPadding, y + dy + config.ReferenceBlockPadding]) / 2 + 127);
        }
    }
    
    private void QuantizeCoefficients(int blockSize, int[,] workmem)
    {
        int[,] Q =
        {
            { 16, 11, 10, 16, 24, 40, 51, 61 },
            { 12, 12, 14, 19, 26, 58, 60, 55 },
            { 14, 13, 16, 24, 40, 57, 69, 56 },
            { 14, 17, 22, 29, 51, 87, 80, 62 },
            { 18, 22, 37, 56, 68, 109, 103, 77 },
            { 24, 35, 55, 64, 81, 104, 113, 92 },
            { 49, 64, 78, 87, 103, 121, 120, 101 },
            { 72, 92, 95, 98, 112, 100, 103, 99 },
        };
    
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            Q[x, y] /= 2;
        
        var subBlocks = blockSize / 8;
        
        for (var yb = 0; yb < subBlocks; yb++)
        for (var xb = 0; xb < subBlocks; xb++)
        {
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                workmem[x + 8*xb, y + 8*yb] /= Q[x, y];
            }
        }
    }
}