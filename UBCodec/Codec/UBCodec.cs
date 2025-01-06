using System.Collections;
using SkiaSharp;

namespace UBCodec.Codec;

public class UBCodec
{

    public int BlockSize { get; set; } = 16;

    public int MotionSearchDist { get; set; } = 5;

    public ITransform Transformer { get; set; } = new DCTInteger1Transform(826);

    public ICoder Coder { get; set; } = new GolombRiceCoder();

    public (BitArray, SKBitmap) EncodeFrame(SKBitmap prev, SKBitmap curr)
    {
        var motionVecs = NeighbourMotionSearch(prev, curr);
        var residual = ComputeResidual(prev, curr, motionVecs);
        var transformed = Transform(residual, inverse: false);
        var coded = Coder.Encode(transformed);
        var compressedBytes = coded.Length / 8.0;
        var uncompressedBytes = (curr.Width * curr.Height * 3.0);
        var sizeRatio = compressedBytes / uncompressedBytes;
        
        Console.WriteLine($"Size reduction: {(Math.Round(sizeRatio * 10000.0)/100.0)}%");
        
        return (coded, motionVecs);
    }
    
    public SKBitmap ReconstructFrame(SKBitmap prev, BitArray coded, SKBitmap motionVecs)
    {
        var transformed = Coder.Decode(coded, prev.Info);
        var residual = Transform(transformed, inverse: true);
        var reconstructed = Reconstruct(prev, residual, motionVecs);
        return reconstructed;
    }

    public SKBitmap NeighbourMotionSearch(SKBitmap prev, SKBitmap curr)
    {
        var output = new SKBitmap(curr.Width / BlockSize, curr.Height / BlockSize, SKColorType.Rgb888x, SKAlphaType.Opaque);
        var D = MotionSearchDist;
        
        for (var yBlock = 0; yBlock < output.Height; yBlock++)
        {
            for (var xBlock = 0; xBlock < output.Width; xBlock++)
            {
                var errorBest = 99999;
                var xBest = 0;
                var yBest = 0;
                
                for (var dx = -D; dx <= D; dx++)
                {
                    for (var dy = -D; dy <= D; dy++)
                    {
                        var error = 0;

                        for (var y = 0; y < BlockSize; y++)
                        {
                            for (var x = 0; x < BlockSize; x++)
                            {
                                if (x % 2 > 0 || y % 2 > 0) continue;
                                
                                var pCurr = curr.GetPixel(xBlock * BlockSize + x, yBlock * BlockSize + y);
                                var pPrev = prev.GetPixel(xBlock * BlockSize + x + dx, yBlock * BlockSize + y + dy);
                                error += Math.Abs(pPrev.Red - pCurr.Red) +
                                   Math.Abs(pPrev.Green - pCurr.Green) +
                                   Math.Abs(pPrev.Blue - pCurr.Blue);
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

                var c = new SKColor(
                    (byte)(xBest + 127),
                    (byte)(yBest + 127),
                    0);
                output.SetPixel(xBlock, yBlock, c);
            }   
        }

        return output;
    }

    public SKBitmap ComputeResidual(SKBitmap prev, SKBitmap curr, SKBitmap blockMotions)
    {
        var output = new SKBitmap(curr.Info);
    
        for (var yb = 0; yb < blockMotions.Height; yb++)
        for (var xb = 0; xb < blockMotions.Width; xb++)
        {
            var maxDist = 0;
            var minDist = 99999;
        
            var blockOffset = blockMotions.GetPixel(xb, yb);
            var xOffset = blockOffset.Red - 127;
            var yOffset = blockOffset.Green - 127;

            for (var y = 0; y < BlockSize; y++)
            for (var x = 0; x < BlockSize; x++)
            {
                var px = xb * BlockSize + x;
                var py = yb * BlockSize + y;
            
                var pCurr = curr.GetPixel(px, py);
                var pPrev = prev.GetPixel(px + xOffset, py + yOffset);

                var c = new SKColor(
                    (byte)((pCurr.Red - pPrev.Red) / 2 + 127),
                    (byte)((pCurr.Green - pPrev.Green) / 2 + 127),
                    (byte)((pCurr.Blue - pPrev.Blue) / 2 + 127)
                );
            
                if (px >= 0 && px < output.Width && py >= 0 && py < output.Height)
                {
                    output.SetPixel(px, py, c);
                }
            }
        }

        return output;
    }
    
    public SKBitmap Transform(SKBitmap input, bool inverse = false)
    {
        var output = new SKBitmap(input.Info);
        int pixelTrans;
    
        int[,] r = new int[8, 8];
        int[,] g = new int[8, 8];
        int[,] b = new int[8, 8];
    
        for (var by = 0; by < input.Height / 8; by++)
        for (var bx = 0; bx < input.Width / 8; bx++)
        {
            pixelTrans = inverse ? 0 : 127;
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var p = input.GetPixel(bx * 8 + x, by * 8 + y);
                r[x, y] = p.Red - pixelTrans;
                g[x, y] = p.Green - pixelTrans;
                b[x, y] = p.Blue - pixelTrans;
            }

            int[,] rt = Transform8x8(r, inverse);
            int[,] gt = Transform8x8(g, inverse);
            int[,] bt = Transform8x8(b, inverse);

            pixelTrans = inverse ? 127 : 0;
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                SKColor c = new SKColor(
                    (byte)(rt[x, y] + pixelTrans),
                    (byte)(gt[x, y] + pixelTrans),
                    (byte)(bt[x, y] + pixelTrans)
                );
                output.SetPixel(bx * 8 + x, by * 8 + y, c);
            }
        }

        return output;
    }

    private int[,] Transform8x8(int[,] input, bool inverse = false)
    {
        if (inverse)
            return Transformer.Transform(QuantizeCoefficients(input, inverse), inverse);
        else
            return QuantizeCoefficients(Transformer.Transform(input, inverse), inverse);
    }
    
    public int[,] QuantizeCoefficients(int[,] input, bool inverse = false)
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
    
        var output = new int[8, 8];
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            if (inverse) output[x, y] = (input[x, y] - 127) * Q[x, y];
            else output[x, y] = input[x, y] / Q[x, y] + 127;
        }

        return output;
    }
    
    public SKBitmap Reconstruct(SKBitmap prev, SKBitmap residual, SKBitmap motionVecs)
    {
        var output = new SKBitmap(prev.Info);

        for (var yb = 0; yb < motionVecs.Height; yb++)
        {
            for (var xb = 0; xb < motionVecs.Width; xb++)
            {
                var blockOffset = motionVecs.GetPixel(xb, yb);
            
                var xOffset = blockOffset.Red - 127;
                var yOffset = blockOffset.Green - 127;

                for (var y = 0; y < BlockSize; y++)
                for (var x = 0; x < BlockSize; x++)
                {
                    var xp = xb * BlockSize + x;
                    var yp = yb * BlockSize + y;
                
                    var p = prev.GetPixel(xp + xOffset, yp + yOffset);
                    var r = residual.GetPixel(xp, yp);
                
                    var c = new SKColor(
                        (byte)(p.Red + (r.Red - 127) * 2),
                        (byte)(p.Green + (r.Green - 127) * 2),
                        (byte)(p.Blue + (r.Blue - 127) * 2)
                    );
                    output.SetPixel(xp, yp, c);
                }
            }
        }

        return output;
    }
}