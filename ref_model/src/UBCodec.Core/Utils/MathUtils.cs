namespace UBCodec.Core.Utils;

public static class MathUtils
{
    public static byte BClamp(int value) => (byte) Math.Clamp(value, 0, 255);
}