using System.Collections;

namespace UBCodec.Codec.NextGen;

public class ByteStreamWriter
{
    private List<Byte> _list = new();

    public byte[] GetArray() => _list.ToArray();
    
    public int Count => _list.Count;
    
    public ByteStreamWriter WriteUInt8(byte v)
    {
        _list.Add(v);
        return this;
    }
    
    public ByteStreamWriter WriteUInt16(ushort v)
    {
        _list.Add((byte)((v >> 0) & 0xFF));
        _list.Add((byte)((v >> 8) & 0xFF));
        return this;
    }

    public ByteStreamWriter WriteBitArray(BitArray array)
    {
        var arrBytes = (array.Count - 1) / 8 + 1;
        for (var i = 0; i < arrBytes; i++)
        {
            byte b = 0;
            for (var j = 0; j < 8; j++)
            {
                var bitIx = i * 8 + j;
                var bit = bitIx < array.Count && array.Get(bitIx);
                b = (byte)((b << 1) | (bit ? 1 : 0));
            }
            _list.Add(b);
        }
        
        return this;
    }

}