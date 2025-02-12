using SkiaSharp;

namespace UBCodec.Codec.NextGen;

public class Utils
{
    public static long Pack8BitValues(byte[] values)
    {
        return (values[0] << 24) | (values[1] << 16) | (values[2] << 8) | values[3];
    }
    
    public static long Pack10BitValues(short[] values)
    {
        return 0L
               | ((values[0] & 0x3FFL) << 30)
               | ((values[1] & 0x3FFL) << 20)
               | ((values[2] & 0x3FFL) << 10)
               | (values[3] & 0x3FFL);
    }
    
    public static byte[] Unpack8BitValues(long data)
    {
        var values = new byte[4];
        values[0] = (byte)((data >> 24) & 0xFF);
        values[1] = (byte)((data >> 16) & 0xFF);
        values[2] = (byte)((data >> 8) & 0xFF);
        values[3] = (byte)((data) & 0xFF);
        return values;
    }
    
    public static short[] Unpack10BitValues(long data)
    {
        var values = new short[4];
        values[0] = (short)((data >> 30) & 0x3FFL);
        values[1] = (short)((data >> 20) & 0x3FFL);
        values[2] = (short)((data >> 10) & 0x3FFL);
        values[3] = (short)((data) & 0x3FFL);
        return values;
    }
    
    public static SKColor ToYUV(SKColor color)
    {
        var r = color.Red;
        var g = color.Green;
        var b = color.Blue;
        
        return new SKColor(
            (byte)(16 + r * 65.738 / 256.0 + g * 129.057 / 256.0 + b * 25.064 / 256.0),
            (byte)(128 - r * 37.945 / 256.0 - g * 74.494 / 256.0 + b * 112.439 / 256.0),
            (byte)(128 + r * 112.439 / 256.0 - g * 94.154 / 256.0 - b * 18.285 / 256.0)
        );
    }
    
    public static SKColor FromYUV(SKColor color)
    {
        var r = color.Red;
        var g = color.Green;
        var b = color.Blue;
        
        return new SKColor(
            (byte)(-222.921 + r * 298.082 / 256.0 + g * 0 + b * 408.583 / 256.0),
            (byte)(+135.576 + r * 298.082 / 256.0 - g * 100.291 / 256.0 - b * 208.120 / 256.0),
            (byte)(-276.836 + r * 298.082 / 256.0 + g * 516.412 / 256.0 - b * 0)
        );
    }
}