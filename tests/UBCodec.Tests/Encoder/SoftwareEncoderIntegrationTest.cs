using CliWrap;
using System.Runtime.InteropServices;
using UBCodec.Core.Encoder;
using static UBCodec.Core.Utils.ImageUtils;

namespace UBCodec.Tests.Encoder;

class EncoderSide(CodecConfig config)
{
    private SoftwareEncoder _encoder = new(config);

    private YCoCgBuffer? _prev;
    
    private YCoCgBuffer? _frameDecoded;

    private int _frameSeq = 0;

    public EncoderSide Initialize(YCoCgBuffer frame)
    {
        _prev = frame;
        return this;
    }

    public (int, int) BufferSize()
    {
        return (_prev.Width, _prev.Height);
    }

    public byte[] Encode(YCoCgBuffer frame)
    {
        _prev ??= YCoCgBuffer.FromSize(frame.Width, frame.Height, _encoder.Config.UVDownsample);
        _frameDecoded ??= YCoCgBuffer.FromSize(frame.Width, frame.Height, _encoder.Config.UVDownsample);

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
    
    private YCoCgBuffer? _prev;
    
    private YCoCgBuffer? _frameDecoded;

    public DecoderSide Initialize(YCoCgBuffer frame)
    {
        _prev = frame;
        return this;
    }

    public YCoCgBuffer Decode(byte[] payload)
    {
        var (frameSeq, width, height) = _encoder.DecodeHeader(new ByteStreamReader(payload));
        
        _prev ??= YCoCgBuffer.FromSize(width, height, _encoder.Config.UVDownsample);
        _frameDecoded ??= YCoCgBuffer.FromSize(width, height, _encoder.Config.UVDownsample);

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

    private static string ffmpeg =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Join(_root, "ffmpeg.exe")
            : "ffmpeg";

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
            BlockSize = 64,
            Quality = 2,
            ReferenceBlockPadding = 0,
            MotionEstimator = new NoopMotionEstimator(),
            DCT = new DctInt1Transform(),
            Coder = new GolombRiceCoder
            {
                // ZigZag = true,
                GolombM = 16
            },
        };

        var frameFiles = await SplitVideo(Path.Join(_root, "resources", "city.mp4"), 4, blockSize: config.BlockSize);
        
        var frame1 = YCoCgBuffer.FromBitmap(ReadPng(frameFiles[0]), config.UVDownsample);
        var frame2 = YCoCgBuffer.FromBitmap(ReadPng(frameFiles[3]), config.UVDownsample);

        var encoder = new EncoderSide(config).Initialize(frame1);
        var decoder = new DecoderSide(config).Initialize(frame1);
        
        var bytes = encoder.Encode(frame2);
        var decoded = decoder.Decode(bytes);
        
        var inputSize = frame1.Width * frame1.Height * 3.0;
        var outputSize = bytes.Length * 1.0;
        
        WritePng(frame2.ToBitmap(), Path.Join(_root, $"output_expect.png"));
        WritePng(decoded.ToBitmap(), Path.Join(_root, $"output_actual.png"));
        
        TestContext.Out.WriteLine($"Compression ratio: {Math.Round(outputSize/inputSize*100.0)}%");
    }

    [Test]
    public async Task VideoTest()
    {
            var config = new CodecConfig
            {
                UVDownsample = 2,
                Quality = 5,
                BlockSize = 32,
                ReferenceBlockPadding = 0,
                MotionEstimator = new NoopMotionEstimator(),
                DCT = new DctInt1Transform(),
                Coder = new GolombRiceCoder
                {
                    GolombM = 8,
                    GolombZM = 8,
                },
            };

            var frameFiles = await SplitVideo(
                Path.Join(_root, "resources", "stockholm_720p.y4m"),
                maxFrames:30,
                scaleDiv:1,
                blockSize:config.BlockSize);

            var encoder = new EncoderSide(config);
            var decoder = new DecoderSide(config);
            var totalBytes = 0;

            for (var i = 0; i < frameFiles.Length; i++)
            {
                var frame = YCoCgBuffer.FromBitmap(ReadPng(frameFiles[i]), config.UVDownsample);
                var bytes = encoder.Encode(frame);
                totalBytes += bytes.Length;
                var frameOut = decoder.Decode(bytes);
                WritePng(frameOut.ToBitmap(), Path.Join(_artifacts, $"rec_{i + 1:D4}.png"));
            }
            
            await StitchVideo("rec_%04d.png", Path.Join(_root, "encoded.mp4"));
            await StitchVideo("rec_%04d.png", Path.Join(_artifacts, "encoded_lossless.mp4"), lossless: true);
            await StitchVideo("frame_%04d.png", Path.Join(_artifacts, "reference_lossless.mp4"), lossless: true);

            var vmafJson = await RunVmaf(
                Path.Join(_artifacts, "reference_lossless.mp4"),
                Path.Join(_artifacts, "encoded_lossless.mp4"));
            PrintVmafSummary(vmafJson);
            
            var uncompressedSize = encoder.BufferSize().Item1 * encoder.BufferSize().Item2 * 3;
            var averageFrameSize = totalBytes / frameFiles.Length;
            var bpp = totalBytes * 8.0 / (encoder.BufferSize().Item1 * encoder.BufferSize().Item2 * frameFiles.Length);
            
            // Console.WriteLine($"- Video stream total size: {totalBytes/1024} kb");
            // Console.WriteLine($"- Average frame size: {averageFrameSize/1024} kb");
            // Console.WriteLine($"- Average compression:  {Math.Round(averageFrameSize*10000.0/uncompressedSize)/100.0}%");
            Console.WriteLine($"- Bits per pixel: {bpp:F3}");

            var grc = (GolombRiceCoder)config.Coder;
            Console.WriteLine($"- Codec: UVDownsample={config.UVDownsample} Quality={config.Quality} BlockSize={config.BlockSize} GolombM={grc.GolombM} GolombZM={grc.GolombZM}");
    }
    
    async Task<string[]> SplitVideo(string inputVideo, int maxFrames, double scaleDiv = 1, int blockSize = 0) {
        var vf = $"fps=4,scale=iw/{scaleDiv}:ih/{scaleDiv}";
        if (blockSize > 0)
            vf += $",crop=iw-mod(iw\\,{blockSize}):ih-mod(ih\\,{blockSize}):0:0";
        await Cli.Wrap(ffmpeg)
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

        await Cli.Wrap(ffmpeg)
            .WithArguments(["-y", "-framerate", "30", "-i", Path.Join(_artifacts, inputPattern), ..codecArgs, "-pix_fmt", "yuv420p", outputVideo])
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync();
        Console.WriteLine($"- Output: {outputVideo} ({new FileInfo(outputVideo).Length / 1024} KB)");
    }

    async Task<string> RunVmaf(string refVideo, string encVideo)
    {
        var jsonPath = Path.Join(_artifacts, "vmaf.json");
        await Cli.Wrap(ffmpeg)
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