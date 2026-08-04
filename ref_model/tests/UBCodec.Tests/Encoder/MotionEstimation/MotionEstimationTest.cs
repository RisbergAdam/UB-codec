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
        var estimator2 = new IntegerMotionVec();
        
        Console.WriteLine(estimator1.Estimate(bigRegion.LBuffer, smallRegion.LBuffer));
        Console.WriteLine(estimator2.Estimate(bigRegion.LBuffer, smallRegion.LBuffer));
    }
    
    [Test]
    public async Task ReferenceTest()
    {
        var frame1 = PlanarImage.FromBitmap(ImageUtils.BlockResize(ImageUtils.ReadPng(await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 100, 
            Path.Join(_root, "artifacts", "frame1.png"))), 32), 1);

        var frame2 = PlanarImage.FromBitmap(ImageUtils.BlockResize(ImageUtils.ReadPng(await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 101,
            Path.Join(_root, "artifacts", "frame2.png"))), 32), 1);

        var xBlocks = frame1.Width / 32;
        var yBlocks = frame1.Height / 32;
        
        var estimator1 = new IntegerMotionRef();
        var estimator2 = new IntegerMotionVec();

            
        for (var xb = 1; xb < xBlocks - 1; xb++)
        for (var yb = 1; yb < yBlocks - 1; yb++)
        {
            var bigRegion = SubpixelSampler.Crop(frame1, xb * 32 - 16, yb * 32 - 16, 64);
            var smallRegion = SubpixelSampler.Crop(frame2, xb * 32, yb * 32, 32);

            var e1 = estimator1.Estimate(bigRegion.LBuffer, smallRegion.LBuffer);
            var e2 = estimator2.Estimate(bigRegion.LBuffer, smallRegion.LBuffer);
            
            Assert.That(e2.X, Is.EqualTo(e1.X));
            Assert.That(e2.Y, Is.EqualTo(e1.Y));
        }
    }
}