using System.Diagnostics;
using System.Drawing;

namespace UBCodec.Core.Encoder;

public class CodecConfig
{
    public int BlockSize { get; set; }
    
    public int ReferenceBlockPadding { get; set; }

    public IBlockMotionEstimator MotionEstimator { get; set; }
    
    public ITransform DCT { get; set; }
    
    public ICoder Coder { get; set; }
}

public class EncoderCore(CodecConfig config)
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

    private int _rows = 0;

    public void LoadBlock(YCoCgBuffer prev, YCoCgBuffer curr, Rectangle region)
    {
        _region = region;
        _rows = curr.Height / _region.Height;

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
    
    public void Encode(ByteStreamWriter byteStream, int frameSeq)
    {
        var streamSize = byteStream.Count;
        var blockMotion = config.MotionEstimator.EstimateMotion(_YBuffer, _YBufferPrev);
        var intraRefresh = (_region.Y / _region.Height) == (frameSeq % _rows);
        
        WriteBlockHeader(byteStream, blockMotion);
        var bytesHeader = byteStream.Count - streamSize;
        streamSize = byteStream.Count;
        
        // Y-channel
        ComputeResidual(_YBuffer, _YBufferPrev, 1, blockMotion, output: _workmem1, intraRefresh);
        config.DCT.TransformForward(config.BlockSize, _workmem1, output: _workmem2);
        QuantizeCoefficients(config.BlockSize, _workmem2);
        config.Coder.Encode(config.BlockSize, _workmem2, output: byteStream);
        var bytesY = byteStream.Count - streamSize;
        streamSize = byteStream.Count;
        
        // Co-channel
        ComputeResidual(_CoBuffer, _CoBufferPrev, 2, blockMotion, output: _workmem1, intraRefresh);
        config.DCT.TransformForward(config.BlockSize/2, _workmem1, output: _workmem2);
        QuantizeCoefficients(config.BlockSize/2, _workmem2);
        config.Coder.Encode(config.BlockSize/2, _workmem2, output: byteStream);
        var bytesCo = byteStream.Count - streamSize;
        streamSize = byteStream.Count;
        
        // Cg-channel
        ComputeResidual(_CgBuffer, _CgBufferPrev, 2, blockMotion, output: _workmem1, intraRefresh);
        config.DCT.TransformForward(config.BlockSize/2, _workmem1, output: _workmem2);
        QuantizeCoefficients(config.BlockSize/2, _workmem2);
        config.Coder.Encode(config.BlockSize/2, _workmem2, output: byteStream);
        var bytesCg = byteStream.Count - streamSize;
    }

    public void Decode(ByteStreamReader byteStream, YCoCgBuffer prev, YCoCgBuffer curr, int frameSeq)
    {
        var (region, blockMotion) = ReadBlockHeader(byteStream);
        LoadBlock(prev, curr, region);
        var intraRefresh = (_region.Y / _region.Height) == (frameSeq % _rows);
        
        // Y-channel
        config.Coder.Decode(config.BlockSize, byteStream, _workmem2);
        QuantizeCoefficients(config.BlockSize, _workmem2, inverse:true);
        config.DCT.TransformInverse(config.BlockSize, _workmem2, output: _workmem1);
        ApplyResidual(_YBufferPrev, _YBuffer, 1, blockMotion, intraRefresh);
        
        // Co-channel
        config.Coder.Decode(config.BlockSize/2, byteStream, _workmem2);
        QuantizeCoefficients(config.BlockSize/2, _workmem2, inverse:true);
        config.DCT.TransformInverse(config.BlockSize/2, _workmem2, output: _workmem1);
        ApplyResidual(_CoBufferPrev, _CoBuffer, 2, blockMotion, intraRefresh);
        
        // Cg-channel
        config.Coder.Decode(config.BlockSize/2, byteStream, _workmem2);
        QuantizeCoefficients(config.BlockSize/2, _workmem2, inverse:true);
        config.DCT.TransformInverse(config.BlockSize/2, _workmem2, output: _workmem1);
        ApplyResidual(_CgBufferPrev, _CgBuffer, 2, blockMotion, intraRefresh);

        StoreBlock(curr);
    }

    private void StoreBlock(YCoCgBuffer target)
    {
        for (var y = 0; y < config.BlockSize; y++)
        for (var x = 0; x < config.BlockSize; x++)
        {
            var sx = x + _region.X;
            var sy = y + _region.Y;
            
            target.YBuffer[sx, sy] = _YBuffer[x, y];
            target.CoBuffer[sx/2, sy/2] = _CoBuffer[x/2, y/2];
            target.CgBuffer[sx/2, sy/2] = _CgBuffer[x/2, y/2];
        }
    }

    private void WriteBlockHeader(ByteStreamWriter stream, MotionEstimate blockMotion)
    {
        stream
            .WriteUInt8((byte)(_region.X / config.BlockSize))
            .WriteUInt8((byte)(_region.Y / config.BlockSize))
            .WriteUInt8((byte)(blockMotion.X + 127))
            .WriteUInt8((byte)(blockMotion.Y + 127));
    }

    private (Rectangle, MotionEstimate) ReadBlockHeader(ByteStreamReader reader)
    {
        return (
            new Rectangle(
                reader.ReadUInt8() * config.BlockSize,
                reader.ReadUInt8() * config.BlockSize,
                config.BlockSize,
                config.BlockSize
            ),
            new MotionEstimate
            {
                X = reader.ReadUInt8() - 127,
                Y = reader.ReadUInt8() - 127
            }
        );
    }

    private void ComputeResidual(byte[,] block, byte[,] blockPrev, int downsample, MotionEstimate blockMotion, byte[,] output, bool intraRefresh)
    {
        var blockSize = block.GetLength(0);
        
        var xOffset = (blockMotion.X + config.ReferenceBlockPadding) / downsample;
        var yOffset = (blockMotion.Y + config.ReferenceBlockPadding) / downsample;
        
        for (var y = 0; y < blockSize; y++)
        for (var x = 0; x < blockSize; x++)
        {
            if (intraRefresh)
            {
                output[x, y] = (byte) Math.Clamp(block[x, y] + 1, 0, 255);
            }
            else
            {
                var currValue =  block[x, y];
                var prevValue = blockPrev[x + xOffset, y + yOffset];
                var residual = currValue - prevValue;
                output[x, y] = (byte) Math.Clamp(residual / 2 + 128, 0, 255);   
            }
        }
    }

    private void ApplyResidual(byte[,] blockPrev, byte[,] block, int downsample, MotionEstimate blockMotion, bool intraRefresh)
    {
        var blockSize = block.GetLength(0);
        
        var xOffset = (blockMotion.X + config.ReferenceBlockPadding) / downsample;
        var yOffset = (blockMotion.Y + config.ReferenceBlockPadding) / downsample;
        
        for (var y = 0; y < blockSize; y++)
        for (var x = 0; x < blockSize; x++)
        {
            if (intraRefresh)
            {
                block[x, y] = _workmem1[x, y];
            }
            else
            {
                var prevValue = blockPrev[x + xOffset, y + yOffset];
                var residual = (_workmem1[x, y] - 127) * 2;
                var currValue = residual + prevValue;
                block[x, y] = (byte) Math.Clamp(currValue, 0, 255);   
            }
        }
    }
    
    private void QuantizeCoefficients(int blockSize, int[,] workmem, bool inverse = false)
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
            Q[x, y] /= 1;
        
        var subBlocks = blockSize / 8;
        
        for (var yb = 0; yb < subBlocks; yb++)
        for (var xb = 0; xb < subBlocks; xb++)
        {
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                if (inverse)
                {
                    workmem[x + 8*xb, y + 8*yb] *= Q[x, y];
                }
                else
                {
                    workmem[x + 8*xb, y + 8*yb] /= Q[x, y];
                }
            }
        }
    }
}