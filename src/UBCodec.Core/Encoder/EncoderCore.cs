using System.Diagnostics;
using System.Drawing;

namespace UBCodec.Core.Encoder;

public class CodecConfig
{
    public int Quality { get; set; } = 1; // 1 - 64
    
    public int BlockSize { get; set; }
    
    public int ReferenceBlockPadding { get; set; }

    public IBlockMotionEstimator MotionEstimator { get; set; }
    
    public ITransform DCT { get; set; }
    
    public ICoder Coder { get; set; }

    public int UVDownsample { get; set; } = 2;
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
    private int _cols = 0;

    int[,] QIntra =
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

    int[,] QInter =
    {
        { 16, 16, 16, 16, 24, 40, 51, 61 },
        { 16, 16, 16, 19, 26, 40, 51, 55 },
        { 16, 16, 16, 24, 40, 51, 51, 56 },
        { 16, 19, 22, 29, 40, 51, 56, 56 },
        { 24, 26, 37, 40, 51, 56, 56, 56 },
        { 40, 40, 40, 40, 56, 56, 56, 56 },
        { 51, 51, 51, 56, 56, 56, 56, 56 },
        { 61, 55, 56, 56, 56, 56, 56, 56 },
    };
    
    public void LoadBlock(YCoCgBuffer prev, YCoCgBuffer curr, Rectangle region)
    {
        _region = region;
        _rows = curr.Height / _region.Height;
        _cols = curr.Width / _region.Width;

        var searchWindowSize = config.BlockSize + config.ReferenceBlockPadding * 2;
        var blockSize = config.BlockSize;
        
        _YBufferPrev = new byte[searchWindowSize, searchWindowSize];
        _CoBufferPrev = new byte[searchWindowSize/config.UVDownsample, searchWindowSize/config.UVDownsample];
        _CgBufferPrev = new byte[searchWindowSize/config.UVDownsample, searchWindowSize/config.UVDownsample];
        
        _YBuffer = new byte[blockSize, blockSize];
        _CgBuffer = new byte[blockSize/config.UVDownsample, blockSize/config.UVDownsample];
        _CoBuffer = new byte[blockSize/config.UVDownsample, blockSize/config.UVDownsample];
        
        _workmem1 =  new byte[blockSize, blockSize];
        _workmem2 =  new int[blockSize, blockSize];
        
        var D = config.UVDownsample;

        for (var y = 0; y < blockSize; y++)
        for (var x = 0; x < blockSize; x++)
        {
            var sx = region.X + x;
            var sy = region.Y + y;
            _YBuffer[x, y] = curr.GetY(sx, sy);
        }

        for (var y = 0; y < blockSize / D; y++)
        for (var x = 0; x < blockSize / D; x++)
        {
            var sx = region.X / D + x;
            var sy = region.Y / D + y;
            _CoBuffer[x, y] = curr.GetCo(sx, sy);
            _CgBuffer[x, y] = curr.GetCg(sx, sy);
        }
        
        for (var y = 0; y < searchWindowSize; y++)
        for (var x = 0; x < searchWindowSize; x++)
        {
            var sx = region.X - config.ReferenceBlockPadding + x;
            var sy = region.Y - config.ReferenceBlockPadding + y;
            _YBufferPrev[x, y] = prev.GetY(sx, sy);
        }

        for (var y = 0; y < searchWindowSize / D; y++)
        for (var x = 0; x < searchWindowSize / D; x++)
        {
            var sx = (region.X - config.ReferenceBlockPadding) / D + x;
            var sy = (region.Y - config.ReferenceBlockPadding) / D + y;
            _CoBufferPrev[x, y] = prev.GetCo(sx, sy);
            _CgBufferPrev[x, y] = prev.GetCg(sx, sy);
        }
    }

    private bool IsIntraRefresh(int frameSeq)
    {
        int colStep = 1;
        var currCol = (_region.X / _region.Width) / colStep;
        var numGroups = (_cols + colStep - 1) / colStep;
        return currCol == (frameSeq % numGroups);
    }
    
    public void Encode(ByteStreamWriter byteStream, int frameSeq)
    {
        
        var blockMotion = config.MotionEstimator.EstimateMotion(_YBuffer, _YBufferPrev);
        var intraRefresh = IsIntraRefresh(frameSeq);
        
        byteStream.SetRegion("BLOCK_HEADER");
        WriteBlockHeader(byteStream, blockMotion);
        
        // Y-channel
        ComputeResidual(_YBuffer, _YBufferPrev, 1, blockMotion, output: _workmem1, intraRefresh);
        config.DCT.TransformForward(config.BlockSize, _workmem1, output: _workmem2);
        QuantizeCoefficients(config.BlockSize, _workmem2, intraRefresh, false);
        byteStream.SetRegion("BLOCK_DATA_Y");
        config.Coder.Encode(config.BlockSize, _workmem2, output: byteStream);
        
        
        // Co-channel
        ComputeResidual(_CoBuffer, _CoBufferPrev, config.UVDownsample, blockMotion, output: _workmem1, intraRefresh);
        config.DCT.TransformForward(config.BlockSize/config.UVDownsample, _workmem1, output: _workmem2);
        QuantizeCoefficients(config.BlockSize/config.UVDownsample, _workmem2, intraRefresh, true);
        byteStream.SetRegion("BLOCK_DATA_CO");
        config.Coder.Encode(config.BlockSize/config.UVDownsample, _workmem2, output: byteStream);
        
        
        // Cg-channel
        ComputeResidual(_CgBuffer, _CgBufferPrev, config.UVDownsample, blockMotion, output: _workmem1, intraRefresh);
        config.DCT.TransformForward(config.BlockSize/config.UVDownsample, _workmem1, output: _workmem2);
        QuantizeCoefficients(config.BlockSize/config.UVDownsample, _workmem2, intraRefresh, true);
        byteStream.SetRegion("BLOCK_DATA_CG");
        config.Coder.Encode(config.BlockSize/config.UVDownsample, _workmem2, output: byteStream);
        
    }

    public void Decode(ByteStreamReader byteStream, YCoCgBuffer prev, YCoCgBuffer curr, int frameSeq)
    {
        var (region, blockMotion) = ReadBlockHeader(byteStream);
        LoadBlock(prev, curr, region);
        var intraRefresh = IsIntraRefresh(frameSeq);
        
        // Y-channel
        config.Coder.Decode(config.BlockSize, byteStream, _workmem2);
        QuantizeCoefficients(config.BlockSize, _workmem2, intraRefresh, false, inverse:true);
        config.DCT.TransformInverse(config.BlockSize, _workmem2, output: _workmem1);
        ApplyResidual(_YBufferPrev, _YBuffer, 1, blockMotion, intraRefresh);
        
        // Co-channel
        config.Coder.Decode(config.BlockSize/config.UVDownsample, byteStream, _workmem2);
        QuantizeCoefficients(config.BlockSize/config.UVDownsample, _workmem2, intraRefresh, true, inverse:true);
        config.DCT.TransformInverse(config.BlockSize/config.UVDownsample, _workmem2, output: _workmem1);
        ApplyResidual(_CoBufferPrev, _CoBuffer, config.UVDownsample, blockMotion, intraRefresh);
        
        // Cg-channel
        config.Coder.Decode(config.BlockSize/config.UVDownsample, byteStream, _workmem2);
        QuantizeCoefficients(config.BlockSize/config.UVDownsample, _workmem2, intraRefresh, true, inverse:true);
        config.DCT.TransformInverse(config.BlockSize/config.UVDownsample, _workmem2, output: _workmem1);
        ApplyResidual(_CgBufferPrev, _CgBuffer, config.UVDownsample, blockMotion, intraRefresh);

        StoreBlock(curr);
    }

    private void StoreBlock(YCoCgBuffer target)
    {
        var D = config.UVDownsample;

        for (var y = 0; y < config.BlockSize; y++)
        for (var x = 0; x < config.BlockSize; x++)
        {
            var sx = x + _region.X;
            var sy = y + _region.Y;
            target.YBuffer[sx, sy] = _YBuffer[x, y];
        }

        for (var y = 0; y < config.BlockSize / D; y++)
        for (var x = 0; x < config.BlockSize / D; x++)
        {
            var sx = x + _region.X / D;
            var sy = y + _region.Y / D;
            target.CoBuffer[sx, sy] = _CoBuffer[x, y];
            target.CgBuffer[sx, sy] = _CgBuffer[x, y];
        }
    }

    private void WriteBlockHeader(ByteStreamWriter stream, MotionEstimate _)
    {
        stream
            .WriteUInt8((byte)(_region.X / config.BlockSize))
            .WriteUInt8((byte)(_region.Y / config.BlockSize));
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
                X = 0,
                Y = 0
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
                output[x, y] = (byte) block[x, y];
            }
            else
            {
                var currValue =  block[x, y];
                var prevValue = blockPrev[x + xOffset, y + yOffset];
                var residual = currValue - (prevValue - (prevValue >> 2));
                output[x, y] = (byte) Math.Clamp(residual + 127, 0, 255);
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
                var residual = _workmem1[x, y] - 127;
                var currValue = residual + (prevValue - (prevValue >> 2));
                block[x, y] = (byte) Math.Clamp(currValue, 0, 255);
            }
        }
    }
    
    private void QuantizeCoefficients(int blockSize, int[,] workmem, bool intraRefresh, bool isChroma, bool inverse = false)
    {
        var Q = intraRefresh ? QIntra : QInter;
        var multiplier = intraRefresh
            ? isChroma ? 6 : 2
            : 7; 
        
        var subBlocks = blockSize / 8;
        
        for (var yb = 0; yb < subBlocks; yb++)
        for (var xb = 0; xb < subBlocks; xb++)
        {
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                int q = Math.Max(1, Q[x, y] * multiplier / config.Quality);
                
                if (inverse)
                {
                    workmem[x + 8*xb, y + 8*yb] *= q;
                }
                else
                {
                    workmem[x + 8*xb, y + 8*yb] /= q;
                }
            }
        }
    }
}