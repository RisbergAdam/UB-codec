namespace UBCodec.Core.Encoder.ColorSpace;

public interface IColorSpace
{
    public (byte, byte, byte) ToSpace(byte r, byte g, byte b);
    
    public (byte, byte, byte) FromSpace(byte x, byte y, byte z);
}