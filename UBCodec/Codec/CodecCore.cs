using System.Drawing;
using SkiaSharp;

namespace UBCodec.Codec;

class CodecCore(CodecConfig config)
{
    private byte[,] _YBufferPrev;
    private byte[,] _CoBufferPrev;
    private byte[,] _CgBufferPrev;
    
    private byte[,] _YBuffer;
    private byte[,] _CoBuffer;
    private byte[,] _CgBuffer;
    
    private byte[,] _buffer;
    
    private Rectangle _region;

    public void LoadBlock(YCoCgBuffer prev, YCoCgBuffer curr, Rectangle region)
    {
        _region = region;

        var searchWindowSize = config.BlockSize + config.MotionSearchDist * 2;
        var blockSize = config.BlockSize;
        
        _YBufferPrev = new byte[searchWindowSize, searchWindowSize];
        _CoBufferPrev = new byte[searchWindowSize/2, searchWindowSize/2];
        _CgBufferPrev = new byte[searchWindowSize/2, searchWindowSize/2];
        
        _YBuffer = new byte[blockSize, blockSize];
        _CgBuffer = new byte[blockSize/2, blockSize/2];
        _CoBuffer = new byte[blockSize/2, blockSize/2];
        _buffer =  new byte[blockSize, blockSize];
        
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
            var sx = region.X - config.MotionSearchDist + x;
            var sy = region.Y - config.MotionSearchDist + y;
            _YBufferPrev[x, y] = prev.GetY(sx, sy);
            _CoBufferPrev[x/2, y/2] = prev.GetCo(sx/2, sy/2);
            _CgBufferPrev[x/2, y/2] = prev.GetCg(sx/2, sy/2);
        }
    }
    
    public void Encode()
    {
        var blockMotion = EstimateBlockMotion();
        ComputeResidual(_YBuffer, _YBufferPrev, blockMotion);
        DCT();
    }

    private SKColor FromYCoCg((byte, byte, byte) YCoCg)
    {
        var (Y, Co, Cg) = YCoCg;
        byte r = (byte) (Y + Co - Cg);
        byte g = (byte) (Y + Cg);
        byte b = (byte) (Y - Cg - Cg);
        return new SKColor(r, g, b);
    }

    private (int, int) EstimateBlockMotion()
    {
        var D = config.MotionSearchDist;
        
        var errorBest = 99999;
        var xBest = 0;
        var yBest = 0;
                
        for (var dx = -D; dx <= D; dx++)
        {
            for (var dy = -D; dy <= D; dy++)
            {
                var error = 0;

                for (var y = 0; y < config.BlockSize; y++)
                {
                    for (var x = 0; x < config.BlockSize; x++)
                    {
                        if (x % 2 > 0 || y % 2 > 0) continue;
                        var YCurr = _YBuffer[x, y];
                        var YPrev = _YBufferPrev[x + dx + D, y + dy + D];
                        error += Math.Abs(YCurr - YPrev);
                    }   
                }
                        
                if (error < errorBest)
                {
                    errorBest = error;
                    xBest = dx;
                    yBest = dy;
                }
            }
        }
        
        return (xBest, yBest);
    }

    private void ComputeResidual(byte[,] buffer, byte[,] prevBuffer, (int, int) motion)
    {
        var (dx, dy) = motion;
        
        for (var y = 0; y < config.BlockSize; y++)
        for (var x = 0; x < config.BlockSize; x++)
        {
            _buffer[x, y] = (byte) ((buffer[x, y] - prevBuffer[x - dx, y - dy]) / 2 + 127);
        }
    }

    private void DCT()
    {
        
    }
}