using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder.ColorSpace;

public class YCoCgColorSpace : IColorSpace
{
    public (byte, byte, byte) ToSpace(byte r, byte g, byte b)
    {
        var Y = (byte) (((r+b) >> 2) + (g >> 1));
        var Co = (byte) (((r-b) >> 1) + 127);
        var Cg = (byte) ((g >> 1) - ((r+b) >> 2) + 127);
        
        return (Y, Co, Cg);
    }

    public (byte, byte, byte) FromSpace(byte Y, byte Co, byte Cg)
    {
        return (
            MathUtils.BClamp(Y + Co - Cg),
            MathUtils.BClamp(Y + (Cg - 127)),
            MathUtils.BClamp(Y - Co - Cg + 254));
    }
}