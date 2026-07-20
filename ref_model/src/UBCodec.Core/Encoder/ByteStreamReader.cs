using System.Collections;

namespace UBCodec.Core.Encoder;

public class ByteStreamReader(byte[] _array)
{
    private int _ix = 0;
    
    public byte ReadUInt8()
    {
        var value = _array[_ix];
        _ix += 1;
        return value;
    }

    public ushort ReadUInt16()
    {
        var value = _array[_ix] | (_array[_ix + 1] << 8);
        _ix += 2;
        return (ushort) value;
    }

    public BitArray ReadBitArray()
    {
        var byteCount = ReadUInt16();
        var bytes = _array.AsSpan().Slice(_ix, byteCount).ToArray();
        var value = new BitArray(bytes);
        _ix += byteCount;
        return value;
    }
}