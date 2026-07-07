using System.Collections;

namespace UBCodec.Core.Encoder;

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
        var bytes = new List<byte>();
        WriteUInt16((ushort) arrBytes);
        for (var i = 0; i < arrBytes; i++)
        {
            byte b = 0;
            for (var j = 0; j < 8 && i * 8 + j < array.Count; j++)
            {
                if (array.Get(i * 8 + j))
                    b |= (byte)(1 << j);
            }
            _list.Add(b);
        }
        return this;
    }
}