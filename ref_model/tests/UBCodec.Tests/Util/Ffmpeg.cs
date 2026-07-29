using CliWrap;
using System.Runtime.InteropServices;

namespace UBCodec.Tests.Util;

public static class Ffmpeg
{
    private static readonly string _root = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "../../../../../.."));

    public static Command Run = Cli.Wrap(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Join(_root, "ffmpeg.exe")
            : "ffmpeg");
    
    public static async Task<string> ExtractFrameAsync(string videoPath, int frameIndex, string outputPath)
    {
        await Ffmpeg.Run
            .WithArguments([
                "-y", "-i", videoPath,
                "-vf", $"select=eq(n\\,{frameIndex})",
                "-vframes", "1",
                outputPath
            ])
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteAsync();

        return outputPath;
    }
    
}