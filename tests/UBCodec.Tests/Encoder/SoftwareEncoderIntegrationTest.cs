using CliWrap;
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

class DecoderSide(CodecConfig config)
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

        if (frameSeq % 5 == 0)
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

    private static string _artifacts = Path.Join(_root, "/artifacts/integration_test");

    [SetUp]
    public void SetUp() => Directory.CreateDirectory(_artifacts);

    [TearDown]
    public void TearDown()
    {
        if (!Directory.Exists(_artifacts)) return;
        Directory.Delete(_artifacts, true);
    }

    [Test]
    public async Task SingleFrameTest()
    {
        var frameFiles = await SplitVideo(Path.Join(_root, "/resources/cars.mp4"), 4);
        
        var config = new CodecConfig
        {
            UVDownsample = 4,
            BlockSize = 32,
            Quality = 4,
            ReferenceBlockPadding = 0,
            MotionEstimator = new NoopMotionEstimator(),
            // MotionEstimator = new BlockMotionEstimatorReference(),
            DCT = new DctInt1Transform(),
            Coder = new GolombRiceCoder
            {
                ZigZag = true,
                GolombM = 16
            },
        };
        
        var frame1 = YCoCgBuffer.FromBitmap(BlockResize(ReadPng(frameFiles[0]), config.BlockSize), config.UVDownsample);
        var frame2 = YCoCgBuffer.FromBitmap(BlockResize(ReadPng(frameFiles[3]), config.BlockSize), config.UVDownsample);

        var encoder = new EncoderSide(config).Initialize(frame1);
        var decoder = new DecoderSide(config).Initialize(frame1);
        
        var bytes = encoder.Encode(frame2);
        var decoded = decoder.Decode(bytes);
        
        var inputSize = frame1.Width * frame1.Height * 3.0;
        var outputSize = bytes.Length * 1.0;
        
        WritePng(decoded.ToBitmap(), Path.Join(_root, $"output.png"));
        
        TestContext.Out.WriteLine($"Compression ratio: {Math.Round(outputSize/inputSize*100.0)}%");
    }

    [Test]
    public async Task VideoTest()
    {
        var frameFiles = await SplitVideo(Path.Join(_root, "/resources/cars.mp4"), 300);
        
        var config = new CodecConfig
        {
            UVDownsample = 4,
            Quality = 1,
            BlockSize = 64,
            ReferenceBlockPadding = 0,
            MotionEstimator = new NoopMotionEstimator(),
            DCT = new DctInt1Transform(),
            Coder = new GolombRiceCoder
            {
                ZigZag = true,
                GolombM = 16
            },
        };

        var encoder = new EncoderSide(config);
        var decoder = new DecoderSide(config);
        var totalBytes = 0;

        for (var i = 0; i < frameFiles.Length; i++)
        {
            var frame = YCoCgBuffer.FromBitmap(BlockResize(ReadPng(frameFiles[i]), config.BlockSize), config.UVDownsample);
            var bytes = encoder.Encode(frame);
            totalBytes += bytes.Length;
            var frameOut = decoder.Decode(bytes);
            WritePng(frameOut.ToBitmap(), Path.Join(_artifacts, $"rec_{i + 1:D4}.png"));
        }
        
        await StitchVideo(Path.Join(_root, $"output_{DateTime.Now}.mp4"));
        
        var uncompressedSize = encoder.BufferSize().Item1 * encoder.BufferSize().Item2 * 3;
        var averageFrameSize = totalBytes / frameFiles.Length;
        
        Console.WriteLine($"- Video stream total size: {totalBytes/1024} kb");
        Console.WriteLine($"- Average frame size: {averageFrameSize/1024} kb");
        Console.WriteLine($"- Average compression:  {Math.Round(averageFrameSize*100.0/uncompressedSize)}%");
    }
    
    async Task<string[]> SplitVideo(string inputVideo, int maxFrames) {
        await Cli.Wrap("ffmpeg")
            .WithArguments([
                "-y", "-i", inputVideo, "-vf", $"fps=30", "-vframes", $"{maxFrames}",
                Path.Join(_artifacts, "frame_%04d.png")
            ])
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync();
        
        return Directory
            .GetFiles(_artifacts, "frame_*.png")
            .OrderBy(f => f)
            .ToArray();
    }

    async Task StitchVideo(string outputVideo)
    {
        await Cli.Wrap("ffmpeg")
            .WithArguments(["-y", "-framerate", "30", "-i", Path.Join(_artifacts, "rec_%04d.png"), "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", outputVideo])
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync();
        Console.WriteLine($"- Output: {outputVideo} ({new FileInfo(outputVideo).Length / 1024} KB)");
    }
}