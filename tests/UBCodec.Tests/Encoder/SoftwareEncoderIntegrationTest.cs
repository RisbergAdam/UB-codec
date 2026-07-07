using CliWrap;
using UBCodec.Core.Encoder;
using static UBCodec.Core.Utils.ImageUtils;

namespace UBCodec.Tests.Encoder;

[Category("Integration")]
[Explicit]
public class SoftwareEncoderIntegrationTest
{
    const int MaxFrames = 30;
    const int Fps = 25;

    string _dir = null!;

    [SetUp]
    public void SetUp() => Directory.CreateDirectory(_dir = Path.GetFullPath("../../../../../artifacts/integration_test"));

    [TearDown]
    public void TearDown()
    {
        if (!Directory.Exists(_dir)) return;
        foreach (var f in Directory.GetFiles(_dir, "frame_*.png").Concat(Directory.GetFiles(_dir, "rec_*.png")))
            File.Delete(f);
    }

    [Test]
    public async Task EncodeDecodeVideoRoundtrip()
    {
        await SplitVideo();
        var frames = Directory.GetFiles(_dir, "frame_*.png").OrderBy(f => f).ToArray();
        Assert.That(frames.Length, Is.GreaterThan(1));

        EncodeDecodeLoop(frames);
        await StitchVideo();

        var output = Path.Combine(_dir, "output.mp4");
        Assert.That(File.Exists(output), Is.True);
        Assert.That(new FileInfo(output).Length, Is.GreaterThan(0));
        TestContext.Out.WriteLine($"Output: {output} ({new FileInfo(output).Length / 1024} KB)");
    }

    async Task SplitVideo() => await Cli.Wrap("ffmpeg")
        .WithArguments(["-y", "-i", Path.GetFullPath("../../../../../resources/cars.mp4"), "-vf", $"fps={Fps}", "-vframes", $"{MaxFrames}", Path.Combine(_dir, "frame_%04d.png")])
        .WithValidation(CommandResultValidation.ZeroExitCode)
        .ExecuteAsync();

    void EncodeDecodeLoop(string[] frames)
    {
        var config = new CodecConfig
        {
            BlockSize = 16,
            ReferenceBlockPadding = 4,
            MotionEstimator = new NoopMotionEstimator(),
            DCT = new DctInt1Transform(),
            Coder = new GolombRiceCoder { GolombM = 32 },
        };
        var enc = new SoftwareEncoder(config);

        var acc = YCoCgBuffer.FromBitmap(BlockResize(ReadPng(frames[0]), config.BlockSize));

        for (var i = 1; i < frames.Length; i++)
        {
            var cur = YCoCgBuffer.FromBitmap(BlockResize(ReadPng(frames[i]), config.BlockSize));
            var data = enc.EncodeFrame(acc, cur, i + 1);
            var rec = YCoCgBuffer.FromSize(cur.Width, cur.Height);
            enc.DecodeFrame(acc, rec, data);
            WritePng(rec.ToBitmap(), Path.Combine(_dir, $"rec_{i + 1:D4}.png"));
            acc = rec;
        }
    }

    async Task StitchVideo() => await Cli.Wrap("ffmpeg")
        .WithArguments(["-y", "-framerate", $"{Fps}", "-i", Path.Combine(_dir, "rec_%04d.png"), "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", Path.Combine(_dir, "output.mp4")])
        .WithValidation(CommandResultValidation.ZeroExitCode)
        .ExecuteAsync();
}