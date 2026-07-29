using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder.ColorSpace;

public class YCbCrColorSpace : IColorSpace
{
    private const int Y_R  =  54;
    private const int Y_G  = 183;
    private const int Y_B  =  18;
    private const int Cb_R = -29;
    private const int Cb_G = -99;
    private const int Cb_B = 128;
    private const int Cr_R = 128;
    private const int Cr_G = -116;
    private const int Cr_B = -12;

    private const int Inv_R_Cr = 403;   // 1.5748 × 256
    private const int Inv_G_Cb =  48;   // 0.1873 × 256
    private const int Inv_G_Cr = 120;   // 0.4681 × 256
    private const int Inv_B_Cb = 475;   // 1.8556 × 256

    public (byte, byte, byte) ToSpace(byte r, byte g, byte b)
    {
        int y  = (Y_R  * r + Y_G  * g + Y_B  * b) >> 8;
        int cb = ((Cb_R * r + Cb_G * g + Cb_B * b) >> 8) + 128;
        int cr = ((Cr_R * r + Cr_G * g + Cr_B * b) >> 8) + 128;

        return (MathUtils.BClamp(y), MathUtils.BClamp(cb), MathUtils.BClamp(cr));
    }

    public (byte, byte, byte) FromSpace(byte y, byte cb, byte cr)
    {
        int cbd = cb - 128;
        int crd = cr - 128;

        int r = y + ((Inv_R_Cr * crd) >> 8);
        int g = y - ((Inv_G_Cb * cbd + Inv_G_Cr * crd) >> 8);
        int b = y + ((Inv_B_Cb * cbd) >> 8);

        return (MathUtils.BClamp(r), MathUtils.BClamp(g), MathUtils.BClamp(b));
    }
}