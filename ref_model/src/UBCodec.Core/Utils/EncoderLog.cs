namespace UBCodec.Core.Utils;

public enum LogLevel { Off = 0, Info = 1, Debug = 2, Trace = 3 }

public static class EncoderLog
{
    public static LogLevel Level { get; set; } = LogLevel.Info;

    public static void Info(string message)
    {
        if (Level >= LogLevel.Info) Console.WriteLine(message);
    }

    public static void Debug(string message)
    {
        if (Level >= LogLevel.Debug) Console.WriteLine(message);
    }

    public static void Trace(string message)
    {
        if (Level >= LogLevel.Trace) Console.WriteLine(message);
    }

    /// <summary>Trace-level output without a trailing newline (for inline dumps).</summary>
    public static void TraceInline(string message)
    {
        if (Level >= LogLevel.Trace) Console.Write(message);
    }
}