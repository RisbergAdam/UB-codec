using SkiaSharp;
using UBCodec.Core.Encoder;
using UBCodec.Core.Utils;
using UBCodec.Tests.Util;

namespace UBCodec.Tests.Encoder;

public class BlockMotionEstimatorTest
{
    private static string _root = Path.GetFullPath("../../../../..");
    
    [Test]
    public async Task SimpleTest1()
    {
        var region = Crop(
            ImageUtils.ReadPng(Path.Join(_root, "resources", "drone_frame.png")),
            64 * 3, 64 * 10, 64
        );
        
        var xOffset = 16 - 3;
        var yOffset = 16 + 1;

        var subRegion = Crop(region, xOffset, yOffset, 32);

        var regionBuffer = PlanarImage.FromBitmap(region, 1);
        var subRegionBuffer = PlanarImage.FromBitmap(subRegion, 1);

        var estimator = new BlockMotionRefEstimator();
        var me = estimator.EstimateMotion(subRegionBuffer.LBuffer, regionBuffer.LBuffer);
        
        Assert.That(me.X, Is.EqualTo(-16 + xOffset));
        Assert.That(me.Y, Is.EqualTo(-16 + yOffset));
    }

    [Test]
    public async Task SimpleTest2()
    {
        var blockSize = 32;
        var padding = (64 - blockSize) / 2;
        
        var frame1 = await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 0, 
            Path.Join(_root, "artifacts", "frame1.png"));
        
        var frame2 = await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 1, 
            Path.Join(_root, "artifacts", "frame2.png"));

        var region = Crop( ImageUtils.ReadPng(frame1), 64 * 3, 64 * 10, 64);
        var subRegion = Crop( ImageUtils.ReadPng(frame2), 64 * 3, 64 * 10, blockSize * 2);

        subRegion = Crop(subRegion, padding, padding, blockSize);
        
        var regionBuffer = PlanarImage.FromBitmap(region, 1);
        var subRegionBuffer = PlanarImage.FromBitmap(subRegion, 1);
        
        var estimator = new BlockMotionRefEstimator();
        var me = estimator.EstimateMotion(subRegionBuffer.LBuffer, regionBuffer.LBuffer);

        var (xf, yf) = ComputeSubpixelOffset(me.X, me.Y, regionBuffer.LBuffer, subRegionBuffer.LBuffer);

        var subRegionPrev = PlanarImage.FromBitmap(Crop(region, padding + me.X, padding + me.Y, blockSize), 1).LBuffer;
        var subRegionPrevF = CropF(regionBuffer.LBuffer, padding + me.X + xf, padding + me.Y + yf, blockSize);
        
        var residual1 = new byte[blockSize, blockSize];
        var residual2 = new byte[blockSize, blockSize];

        var error1 = 0;
        var error2 = 0;
        
        for (int y = 0; y < blockSize; y++)
        for (int x = 0; x < blockSize; x++)
        {
            var r1 = subRegionBuffer.LetL(x, y) - regionBuffer.LBuffer[padding + x + me.X, padding + y + me.Y];
            var r2 = subRegionBuffer.LetL(x, y) - subRegionPrevF[x, y];
            
            residual1[x, y] = (byte)Math.Clamp(r1 + 127, 0, 255);
            residual2[x, y] = (byte) Math.Clamp(r2 + 127, 0, 255);

            error1 += Math.Abs(r1);
            error2 += Math.Abs(r2);
        }

        var trans = new DctInt1Transform();
        
        var output1 = new int[blockSize, blockSize];
        var output2 = new int[blockSize, blockSize];
        
        trans.TransformForward(blockSize, residual1, output1);
        trans.TransformForward(blockSize, residual2, output2);
        
        for (int y = 0; y < blockSize; y++)
        for (int x = 0; x < blockSize; x++)
        {
            output1[x, y] /= 10;
            output2[x, y] /= 10;
        }

        var blocks1 = Split8x8(output1);
        var blocks2 = Split8x8(output2);
        var coefs1tot = 0;
        var coefs2tot = 0;

        for (var i = 0; i < blocks1.Count; i++)
        {
            var coefs1 = blocks1[i].Cast<int>().Count(x => x != 0);
            var coefs2 = blocks2[i].Cast<int>().Count(x => x != 0);
            Console.WriteLine($"coefs1: {coefs1}, coefs2: {coefs2}");
            coefs1tot += coefs1;
            coefs2tot += coefs2;
        }
        
        Console.WriteLine($"coefs1tot: {coefs1tot} coefs2tot: {coefs2tot}");
        
        ImageUtils.WritePng(region, Path.Join(_root, "artifacts", "frame1.png"));
        ImageUtils.WritePng(subRegion, Path.Join(_root, "artifacts", "frame2.png"));

        Console.WriteLine($"X: {me.X}, Y: {me.Y}, Error: {me.Error}");
    }

    [Test]
    public async Task ResampleTest()
    {
        var region = Crop(ImageUtils.ReadPng(Path.Join(_root, "resources", "drone_frame.png")), 0, 600, 200);
        
        var buffer = PlanarImage.FromBitmap(region, 1);
        buffer.LBuffer = CropF(buffer.LBuffer, 10.5f, 10.5f, 180);
        buffer.C1Buffer = CropF(buffer.C1Buffer, 10.5f, 10.5f, 180);
        buffer.C2Buffer = CropF(buffer.C2Buffer, 10.5f, 10.5f, 180);
        buffer.Width = 180;
        buffer.Height = 180;

        var region2 = buffer.ToBitmap();
        
        ImageUtils.WritePng(region, Path.Join(_root, "artifacts", "region1.png"));
        ImageUtils.WritePng(region2, Path.Join(_root, "artifacts", "region2.png"));
    }

    [Test]
    public async Task ErrorTest()
    {
        var blockSize = 8;
        
        var frame1 = PlanarImage.FromBitmap(ImageUtils.BlockResize(ImageUtils.ReadPng(await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 100, 
            Path.Join(_root, "artifacts", "frame1.png"))), blockSize), 1);

        var frame2 = PlanarImage.FromBitmap(ImageUtils.BlockResize(ImageUtils.ReadPng(await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 101,
            Path.Join(_root, "artifacts", "frame2.png"))), blockSize), 1);

        var xBlocks = frame1.Width / blockSize;
        var yBlocks = frame1.Height / blockSize;
        
        for (int xb = 0; xb < xBlocks; xb++)
        for (int yb = 0; yb < yBlocks; yb++)
        {
            var error = 0;
            for (int x = 0; x < blockSize; x++)
            for (int y = 0; y < blockSize; y++)
            {
                error += Math.Abs(
                    frame1.LBuffer[x + xb * blockSize, y + yb * blockSize]
                    - frame2.LBuffer[x + xb * blockSize, y + yb * blockSize]);
            }
            Console.WriteLine($"block: {xb}, {yb} error: {error}, EPP: {error*1f/blockSize/blockSize:F3}");
        }
    }

    private (float, float) ComputeSubpixelOffset(int xOffsetI, int yOffsetI, byte[,] region, byte[,] subregion)
    {
        // compute gradients
        var size = subregion.GetLength(0);
        var padding = (region.GetLength(0) - size) / 2;
        var Ix = new int[size-2, size-2];
        var Iy = new int[size-2, size-2];
        var It = new int[size-2, size-2];

        for (var y = 0; y < size - 2; y++)
        for (var x = 0; x < size - 2; x++)
        {
            var x1 = 1 + x; var y1 = 1 + y;
            Ix[x, y] = (subregion[x1 - 1, y1] - subregion[x1 + 1, y1])/2;
            Iy[x, y] = (subregion[x1, y1 - 1] - subregion[x1, y1 + 1])/2;
            It[x, y] = subregion[x1, y1] - region[padding + x1 - xOffsetI, padding + y1 - yOffsetI];
        }
        
        // accumulate gradients
        float Sxx = 0.0f;
        float Syy = 0.0f;
        float Sxy = 0.0f;
        float Sxt = 0.0f;
        float Syt = 0.0f;

        // 1. Accumulate gradient products over the N x N block
        for (int y = 0; y < size-2; y++)
        for (int x = 0; x < size-2; x++)
        {
            float ix = Ix[x, y];
            float iy = Iy[x, y];
            float it = It[x, y];

            Sxx += ix * ix;
            Syy += iy * iy;
            Sxy += ix * iy;
            Sxt += ix * it;
            Syt += iy * it;
        }

        // 2. Compute the determinant of the 2x2 ATA matrix
        float det = (Sxx * Syy) - (Sxy * Sxy);
        float invDet = 1.0f / det;

        float deltaX = (-Syy * Sxt + Sxy * Syt) * invDet;
        float deltaY = ( Sxy * Sxt - Sxx * Syt) * invDet;

        // 5. Clamp the result: fractional offset from integer match should be within [-1.0, +1.0]
        deltaX = Math.Clamp(deltaX, -1.0f, 1.0f);
        deltaY = Math.Clamp(deltaY, -1.0f, 1.0f);
        
        float quarterPelX = MathF.Round(deltaX * 4.0f) / 4.0f;
        float quarterPelY = MathF.Round(deltaY * 4.0f) / 4.0f;

        return (quarterPelX, quarterPelY);
    }

    private SKBitmap Crop(SKBitmap bitmap, int X, int Y, int size)
    {
        var region = new SKBitmap(size, size);
        bitmap.ExtractSubset(region, new SKRectI(X, Y, X + size, Y + size));
        return region;
    }

    private byte[,] CropF(byte[,] input, float x, float y, int size)
    {
        int srcHeight = input.GetLength(0);
        int srcWidth = input.GetLength(1);

        // 1. Separate into integer base coordinates and fractional offsets
        int intX = (int)MathF.Floor(x);
        int intY = (int)MathF.Floor(y);

        float fracX = x - intX;
        float fracY = y - intY;

        // Map sub-pixel offset to nearest 1/4-pel index [0..3]
        int phaseX = (int)MathF.Round(fracX * 4.0f) & 3;
        int phaseY = (int)MathF.Round(fracY * 4.0f) & 3;

        // Adjust integer base if rounding wrapped around (i.e. frac rounded to 1.0)
        intX += (int)MathF.Round(fracX * 4.0f) >> 2;
        intY += (int)MathF.Round(fracY * 4.0f) >> 2;

        // 8-tap DCT-IF filter coefficients for 1/4-pel phases (normalized to sum to 64)
        var filter8Tap = new int[][]
        {
            [0,  0,   0, 64,  0,  0,  0,  0], // Phase 0 (Integer)
            [-1,  4, -10, 58, 17, -5,  1,  0], // Phase 1 (1/4-pel)
            [-1,  4, -11, 40, 40,-11,  4, -1], // Phase 2 (1/2-pel)
            [0,  1,  -5, 17, 58,-10,  4, -1] // Phase 3 (3/4-pel)
        };

        int[] coeffX = filter8Tap[phaseX];
        int[] coeffY = filter8Tap[phaseY];
        
        // Intermediate buffer for 1D horizontal pass: needs 7 extra rows for vertical 8-tap span
        int[,] intermediate = new int[size + 7, size];

        // Helper lambda for frame edge-clamping (sample extension)
        int Clamp(int v, int max) => Math.Clamp(v, 0, max - 1);

        // 2. Horizontal Pass (Filter across rows)
        for (int r = -3; r < size + 4; r++)
        {
            int clampedY = Clamp(intY + r, srcHeight);
            int interRowIdx = r + 3;

            for (int c = 0; c < size; c++)
            {
                int sum = 0;
                for (int k = 0; k < 8; k++)
                {
                    int sampleX = Clamp(intX + c - 3 + k, srcWidth);
                    sum += coeffX[k] * input[sampleX, clampedY];
                }
                // Keep internal precision (shift by 2, leaving factor of 16 for vertical pass)
                intermediate[interRowIdx, c] = sum >> 2; 
            }
        }

        // 3. Vertical Pass (Filter down columns)
        var result = new byte[size, size];
        
        for (int r = 0; r < size; r++)
        for (int c = 0; c < size; c++)
        {
            int sum = 0;
            for (int k = 0; k < 8; k++)
            {
                sum += coeffY[k] * intermediate[r + k, c];
            }

            // Divide out the combined filter gain (64 * 16 = 1024 = 2^10) with rounding
            int val = (sum + 512) >> 10;
            
            // Output clamp (e.g. 8-bit range [0, 255])
            result[c, r] = (byte) Math.Clamp(val, 0, 255);
        }

        return result;
    }

    private static List<int[,]> Split8x8(int[,] input)
    {
        var subBlocks = input.GetLength(0) / 8;
        var list = new List<int[,]>();
        
        for (var yb = 0; yb < subBlocks; yb++)
        for (var xb = 0; xb < subBlocks; xb++)
        {
            var block = new int[8, 8];
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                block[x, y] = input[x + 8 * xb, y + 8 * yb];
            }

            list.Add(block);
        }

        return list;
    }
}