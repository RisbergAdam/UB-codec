# AGENTS.md - UB-codec Project Guide

## Project Overview

UB-codec is a custom video codec implementation in C# targeting .NET 9.0. It's a research/educational codec that processes video frames in a block-based manner with motion estimation, DCT transform, quantization, and entropy coding. The project does not use standard codecs like H.264.

## Project Structure

```
UB-codec/
├── AGENTS.md                         # This file
├── README.md                         # Project documentation
├── UBCodec.sln                       # Solution file
├── global.json                       # Pins .NET 9.0.100 SDK
├── .gitignore
├── ffmpeg_create_video.sh            # FFmpeg utility script
├── src/
│   ├── UBCodec.Core/                 # Class library (encoder + utils)
│   │   ├── UBCodec.Core.csproj       # .NET 9.0, SkiaSharp 3.118.0-preview.2.3
│   │   ├── Encoder/
│   │   │   ├── SoftwareEncoder.cs    # Main codec (encode/decode frames)
│   │   │   ├── EncoderCore.cs        # Core encoding logic with DCT/quantization
│   │   │   ├── DCTInt1Transform.cs   # Integer DCT implementation
│   │   │   ├── GolombRiceCoder.cs    # Golomb-Rice entropy coding
│   │   │   ├── BlockMotionEstimatorReference.cs  # Motion estimation
│   │   │   ├── YCoCgBuffer.cs        # Color space conversion
│   │   │   ├── ByteStreamReader.cs   # Byte/bit stream reading
│   │   │   ├── ByteStreamWriter.cs   # Byte/bit stream writing
│   │   │   ├── IBlockMotionEstimator.cs  # Motion estimator interface
│   │   │   ├── ICoder.cs             # Coder interface
│   │   │   └── ITransform.cs         # Transform interface
│   │   └── Utils/
│   │       ├── ImageUtils.cs         # Image processing utilities
│   │       ├── BitList.cs            # Bit manipulation
│   │       └── ArrayUtils.cs         # Array utilities
│   └── UBCodec.App/                  # Console app (entry point)
│       ├── Program.cs
│       └── UBCodec.App.csproj
├── tests/
│   └── UBCodec.Tests/                # NUnit test project
│       ├── UBCodec.Tests.csproj
│       └── Encoder/
│           ├── DCTInt1TransformTest.cs
│           ├── GolombRiceCoderTest.cs
│           └── YCoCgBufferTest.cs
├── resources/                        # Test videos and static assets
│   ├── cars.mp4                      # Test video
│   └── input_cars/                   # Frame PNGs (frame_1.png ... frame_307.png)
└── artifacts/                        # Temporary outputs (gitignored)
```

## Build & Test Commands

```bash
# Build the solution (requires .NET 9 SDK on PATH)
dotnet build

# Run all tests
dotnet test

# Run a specific test
dotnet test --filter "DCTInt1TransformTest"
```

## Architecture

### Codec Pipeline (SoftwareEncoder)

1. **Input**: RGB image (via SkiaSharp)
2. **Color Space**: RGB → YCoCg (luminance + chrominance)
3. **Chroma Downsampling**: 2x downsampling of Co and Cg channels
4. **Block Processing**: 16x16 blocks (configurable)
5. **Motion Estimation**: Full search with SAD metric (optional)
6. **Residual Computation**: Current - Reference (or zero for I-frames)
7. **DCT Transform**: 8x8 integer DCT
8. **Quantization**: Fixed quantization in SoftwareEncoder, JPEG-style Q tables in EncoderCore
9. **Entropy Coding**: Golomb-Rice with RLE preprocessing
10. **Output**: Bitstream
