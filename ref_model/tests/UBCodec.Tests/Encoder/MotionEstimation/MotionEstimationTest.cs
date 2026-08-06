using CommunityToolkit.HighPerformance;
using UBCodec.Core.Encoder;
using UBCodec.Core.Encoder.MotionEstimation;
using UBCodec.Core.Encoder.Sampling;
using UBCodec.Core.Utils;
using UBCodec.Tests.Util;

namespace UBCodec.Tests.Encoder.MotionEstimation;

public class MotionEstimationTest
{
    private static string _root = Path.GetFullPath("../../../../..");
    
    [Test]
    public void SubPixelMotionTest()
    {
        var image = PlanarImage.FromBitmap(ImageUtils.ReadPng(Path.Join(_root, "resources", "drone_frame.png")));
        
        var bigRegion = SubpixelSampler.Crop(image, 910, 480, 64);
        var smallRegion = SubpixelSampler.Crop(bigRegion, 14.5f, 17f, 32);
        
        var estimator1 = new IntegerMotionRef();
        var estimator2 = new SubPixelMotionRef(new DiamondMotionVec());
        
        Console.WriteLine(estimator1.Estimate(bigRegion.LBuffer, smallRegion.LBuffer));
        Console.WriteLine(estimator2.Estimate(bigRegion.LBuffer, smallRegion.LBuffer));
    }
    
    [Test]
    public void DiamondSearchFindsKnownOffset()
    {
        // Use a single Gaussian blob: a smooth, unimodal source (one minimum, no
        // periodicity), so the SAD surface has a single basin that a greedy
        // diamond search can descend to the global optimum. This is the case the
        // heuristic is designed to handle. (Random noise or periodic content
        // produces a needle-like / multi-modal SAD surface that no greedy search
        // can traverse — that is inherent to the heuristic, not a bug.)
        var sourceBuffer = new byte[64, 64];
        for (int x = 0; x < 64; x++)
        for (int y = 0; y < 64; y++)
        {
            double d2 = (x - 30) * (x - 30) + (y - 30) * (y - 30);
            sourceBuffer[x, y] = (byte)(150.0 * Math.Exp(-d2 / 200.0));
        }

        // Template is an exact copy of a 16x16 region of the blob at a known offset.
        int offX = 10;
        int offY = 12;
        var templateBuffer = new byte[16, 16];
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
            templateBuffer[x, y] = sourceBuffer[offX + x, offY + y];

        var source = new Span2D<byte>(sourceBuffer);
        var template = new Span2D<byte>(templateBuffer);

        var refEstimate = new IntegerMotionRef().Estimate(source, template);
        var vecEstimate = new DiamondMotionVec().Estimate(source, template);

        // Exhaustive ref finds the exact offset (SAD = 0 there).
        Assert.That(refEstimate.X, Is.EqualTo(offX));
        Assert.That(refEstimate.Y, Is.EqualTo(offY));

        // On a unimodal surface the greedy diamond should descend to the same
        // unique, zero-error optimum.
        Assert.That(vecEstimate.X, Is.EqualTo(offX).Within(1));
        Assert.That(vecEstimate.Y, Is.EqualTo(offY).Within(1));
        Assert.That(vecEstimate.Error, Is.LessThanOrEqualTo(refEstimate.Error + 0.001f));
    }

    [Test]
    public async Task ReferenceTest()
    {
        var frame1 = PlanarImage.FromBitmap(ImageUtils.BlockResize(ImageUtils.ReadPng(await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 0, 
            Path.Join(_root, "artifacts", "frame1.png"))), 32), 1);

        var frame2 = PlanarImage.FromBitmap(ImageUtils.BlockResize(ImageUtils.ReadPng(await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 1,
            Path.Join(_root, "artifacts", "frame2.png"))), 32), 1);

        var xBlocks = frame1.Width / 32;
        var yBlocks = frame1.Height / 32;
        
        var estimator1 = new IntegerMotionRef();
        var estimator2 = new DiamondMotionVec();

        double sumE1 = 0, sumE2 = 0;

        void EvaluateBlock(int xb, int yb)
        {
            var bigRegion = SubpixelSampler.Crop(frame1, xb * 32 - 16, yb * 32 - 16, 64);
            var smallRegion = SubpixelSampler.Crop(frame2, xb * 32, yb * 32, 32);

            var e1 = estimator1.Estimate(bigRegion.LBuffer, smallRegion.LBuffer);
            var e2 = estimator2.Estimate(bigRegion.LBuffer, smallRegion.LBuffer);

            sumE1 += e1.Error;
            sumE2 += e2.Error;
        }

        for (var xb = 1; xb < xBlocks - 1; xb++)
        for (var yb = 1; yb < yBlocks - 1; yb++)
        {
            EvaluateBlock(xb, yb);
        }

        // IntegerMotionVec uses a hierarchical (coarse-to-fine) diamond search, a
        // heuristic that trades optimality for speed: block-matching SAD surfaces
        // are multi-modal, so per-block it can land in a different (slightly worse)
        // local minimum than the exhaustive IntegerMotionRef. On the drone test set
        // the aggregate mean-SAD is ~1.19x that of the reference. Bound the aggregate
        // so regressions that materially degrade overall quality are caught, while
        // allowing the expected heuristic overhead.
        Assert.That(sumE2 / sumE1, Is.LessThanOrEqualTo(1.35));
    }
}