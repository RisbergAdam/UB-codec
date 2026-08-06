using CliWrap;
using UBCodec.Core.Encoder;
using UBCodec.Core.Encoder.MotionEstimation;
using UBCodec.Core.Utils;
using UBCodec.Tests.Util;
using static UBCodec.Core.Utils.ImageUtils;

namespace UBCodec.Tests.Encoder;

class EncoderSide(CodecConfig config)
{
    private SoftwareEncoder _encoder = new(config);

    public PlanarImage? _prev;
    
    private PlanarImage? _frameDecoded;

    private int _frameSeq = 0;

    public EncoderSide Initialize(PlanarImage frame)
    {
        _prev = frame;
        return this;
    }

    public (int, int) BufferSize()
    {
        return (_prev.Width, _prev.Height);
    }

    public byte[] Encode(PlanarImage frame)
    {
        _prev ??= PlanarImage.FromSize(frame.Width, frame.Height, _encoder.Config.UVDownsample);
        _frameDecoded ??= PlanarImage.FromSize(frame.Width, frame.Height, _encoder.Config.UVDownsample);

        var data = _encoder.EncodeFrame(_prev, frame, _frameSeq);
        _encoder.DecodeFrame(_prev, _frameDecoded, data);
        (_frameDecoded, _prev) = (_prev, _frameDecoded);
        _frameSeq++;

        return data;
    }
}

class DecoderSide(CodecConfig config, bool simulateFrameDrops = false)
{
    private SoftwareEncoder _encoder = new(config);
    
    private PlanarImage? _prev;
    
    private PlanarImage? _frameDecoded;

    public DecoderSide Initialize(PlanarImage frame)
    {
        _prev = frame;
        return this;
    }

    public PlanarImage Decode(byte[] payload)
    {
        var (frameSeq, width, height) = _encoder.DecodeHeader(new ByteStreamReader(payload));
        
        _prev ??= PlanarImage.FromSize(width, height, _encoder.Config.UVDownsample);
        _frameDecoded ??= PlanarImage.FromSize(width, height, _encoder.Config.UVDownsample);

        if (simulateFrameDrops && frameSeq % 5 == 0)
        {
            // Simulate frame drop
        }
        else
        {
            _encoder.DecodeFrame(_prev, _frameDecoded, payload);
            (_frameDecoded, _prev) = (_prev, _frameDecoded);   
        }

        return _prev;
    }
}

[Category("Integration")]
[Explicit]
public class SoftwareEncoderIntegrationTest
{
    private static string _root = Path.GetFullPath("../../../../..");

    private static string _artifacts = Path.Join(_root, "artifacts", "integration_test");

    [SetUp]
    public void SetUp()
    {
        if (Directory.Exists(_artifacts))
        {
            Directory.Delete(_artifacts, true);
        }
        
        Directory.CreateDirectory(_artifacts);
    }

    [Test]
    public async Task SingleFrameTest()
    {        
        var config = new CodecConfig
        {
            UVDownsample = 2,
            BlockSize = 16,
            Quality = 2,
            ReferenceBlockPadding = 8,
            EstimateMotion = true,
            // MotionEstimator = new IntegerMotionVec(),
            MotionEstimator = new SubPixelMotionRef(new DiamondMotionVec()),
            DCT = new DctInt1Transform(),
            Coder = new GolombRiceCoder
            {
                GolombM = 4,
                GolombZM = 8,
            },
            LogLevel = LogLevel.Trace
        };

        var frame1 = PlanarImage.FromBitmap(ImageUtils.BlockResize(ImageUtils.ReadPng(await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 0, 
            Path.Join(_root, "artifacts", "frame1.png"))), 32), config.UVDownsample);

        var frame2 = PlanarImage.FromBitmap(ImageUtils.BlockResize(ImageUtils.ReadPng(await Ffmpeg.ExtractFrameAsync(
            Path.Join(_root, "resources", "drone.mp4"), 1,
            Path.Join(_root, "artifacts", "frame2.png"))), 32), config.UVDownsample);

        var encoder = new EncoderSide(config).Initialize(frame1);
        var decoder = new DecoderSide(config).Initialize(frame1);
        
        var bytes = encoder.Encode(frame2);
        var decoded = decoder.Decode(bytes);

        var fm = DistortionollectMetrics(frame2, decoded, bytes.Length);

        WritePng(frame2.ToBitmap(), Path.Join(_root, $"output_expect.png"));
        WritePng(decoded.ToBitmap(), Path.Join(_root, $"output_actual.png"));
        
        Console.WriteLine($"PBB: {fm.Bpp:F3}");
        Console.WriteLine($"Luma PSNR: {fm.LumaPsnr:F2} dB");
        Console.WriteLine($"Weighted PSNR: {fm.WeightedPsnr:F2} dB");
    }

    [Test]
    public async Task VideoTest()
    {
        var m = 4; var zm = 8;
        // foreach (var m in new List<int>([2, 4, 8, 16, 32, 64]))
        // foreach (var zm in new List<int>([2, 4, 8, 16, 32, 64]))
        {
            TestContext.Progress.WriteLine($"===== TEST M {m} ZM {zm} =====");
            var config = new CodecConfig
            {
                UVDownsample = 2,
                Quality = 4,
                BlockSize = 16,
                ReferenceBlockPadding = 8,
                EstimateMotion = true,
                MotionEstimator = new SubPixelMotionRef(new DiamondMotionVec()),
                DCT = new DctInt1Transform(),
                Coder = new GolombRiceCoder
                {
                    GolombM = m,
                    GolombZM = zm,
                },
                LogLevel = LogLevel.Off
            };

            var frameFiles = await SplitVideo(
                Path.Join(_root, "resources", "drone.mp4"),
                maxFrames:60,
                scaleDiv:2,
                blockSize:config.BlockSize);

            var encoder = new EncoderSide(config);
            var decoder = new DecoderSide(config, simulateFrameDrops:false);
            var totalBytes = 0;

            for (var i = 0; i < frameFiles.Length; i++)
            {
                TestContext.Progress.WriteLine($"frame {i}/{frameFiles.Length}");
                var frame = PlanarImage.FromBitmap(ReadPng(frameFiles[i]), config.UVDownsample);
                var bytes = encoder.Encode(frame);
                totalBytes += bytes.Length;
                var frameOut = decoder.Decode(bytes);
                WritePng(frameOut.ToBitmap(), Path.Join(_artifacts, $"rec_{i + 1:D4}.png"));

                var fm = Distortion.CollectMetrics(frame, frameOut, bytes.Length);
                TestContext.Progress.WriteLine(
                    $"- frame {i}: bpp={fm.Bpp:F3} lumaPSNR={fm.LumaPsnr:F2} dB weightedPSNR={fm.WeightedPsnr:F2} dB");

                var bpp = totalBytes * 8.0 / (encoder.BufferSize().Item1 * encoder.BufferSize().Item2 * i);
                TestContext.Progress.WriteLine($"- Bits per pixel: {bpp:F3}");
            }
            
            await StitchVideo("rec_%04d.png", Path.Join(_root, "encoded.mp4"));
            await StitchVideo("rec_%04d.png", Path.Join(_artifacts, "encoded_lossless.mp4"), lossless: true);
            await StitchVideo("frame_%04d.png", Path.Join(_artifacts, "reference_lossless.mp4"), lossless: true);

            var vmafJson = await RunVmaf(
                Path.Join(_artifacts, "reference_lossless.mp4"),
                Path.Join(_artifacts, "encoded_lossless.mp4"));
            PrintVmafSummary(vmafJson);
            

            var grc = (GolombRiceCoder)config.Coder;
            Console.WriteLine($"- Codec: UVDownsample={config.UVDownsample} Quality={config.Quality} BlockSize={config.BlockSize} GolombM={grc.GolombM} GolombZM={grc.GolombZM}");
        }
    }
    
    async Task<string[]> SplitVideo(string inputVideo, int maxFrames, double scaleDiv = 1, int blockSize = 0) {
        var vf = $"fps=60,scale=iw/{scaleDiv}:ih/{scaleDiv}";
        if (blockSize > 0)
            vf += $",crop=iw-mod(iw\\,{blockSize}):ih-mod(ih\\,{blockSize}):0:0";
        await Ffmpeg.Run
            .WithArguments([
                "-y", "-i", inputVideo, "-vf", vf, "-vframes", $"{maxFrames}",
                Path.Join(_artifacts, "frame_%04d.png")
            ])
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync();
        
        return Directory
            .GetFiles(_artifacts, "frame_*.png")
            .OrderBy(f => f)
            .ToArray();
    }

    async Task StitchVideo(string inputPattern, string outputVideo, bool lossless = false)
    {
        var codecArgs = lossless
            ? new[] { "-c:v", "ffv1" }
            : new[] { "-c:v", "libx264", "-crf", "18" };

        await Ffmpeg.Run
            .WithArguments(["-y", "-framerate", "60", "-i", Path.Join(_artifacts, inputPattern), ..codecArgs, "-pix_fmt", "yuv420p", outputVideo])
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync();
        Console.WriteLine($"- Output: {outputVideo} ({new FileInfo(outputVideo).Length / 1024} KB)");
    }

    async Task<string> RunVmaf(string refVideo, string encVideo)
    {
        var jsonPath = Path.Join(_artifacts, "vmaf.json");
        await Ffmpeg.Run
            .WithArguments([
                "-y", "-i", encVideo, "-i", refVideo,
                "-lavfi", $"libvmaf=log_path={jsonPath}:log_fmt=json:n_threads=4",
                "-f", "null", "-"
            ])
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();
        return jsonPath;
    }

    void PrintVmafSummary(string jsonPath)
    {
        TestContext.Out.WriteLine($"- VMAF report: {jsonPath}");
        if (!File.Exists(jsonPath)) return;
        var json = File.ReadAllText(jsonPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("pooled_metrics", out var metrics) &&
            metrics.TryGetProperty("vmaf", out var vmaf))
        {
            var harmonic = vmaf.GetProperty("harmonic_mean").GetDouble();
            TestContext.Out.WriteLine($"- VMAF harmonic mean: {harmonic:F2}");
        }

        if (root.TryGetProperty("frames", out var frames))
        {
            double sum = 0;
            int count = 0;
            foreach (var f in frames.EnumerateArray())
            {
                if (count++ == 0) continue; // skip frame 0
                sum += f.GetProperty("metrics").GetProperty("vmaf").GetDouble();
            }
            var mean = sum / (count - 1);
            TestContext.Out.WriteLine($"- VMAF arithmetic mean: {mean:F2}");
        }
    }
}