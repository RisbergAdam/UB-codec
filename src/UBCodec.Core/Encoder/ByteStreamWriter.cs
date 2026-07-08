using System.Collections;

namespace UBCodec.Core.Encoder;

public class ByteStreamWriter
{
    private List<Byte> _list = new();

    private string _region = null;

    private Dictionary<string, int> _regionBytes = [];

    public byte[] GetArray() => _list.ToArray();
    
    public int Count => _list.Count;

    public void SetRegion(string region)
    {
        _regionBytes.TryAdd(region, 0);
        _region = region;
    }
    
    public ByteStreamWriter WriteUInt8(byte v)
    {
        _list.Add(v);
        _regionBytes[_region] += 1;
        return this;
    }
    
    public ByteStreamWriter WriteUInt16(ushort v)
    {
        _list.Add((byte)((v >> 0) & 0xFF));
        _list.Add((byte)((v >> 8) & 0xFF));
        _regionBytes[_region] += 2;
        return this;
    }

    public ByteStreamWriter WriteBitArray(BitArray array)
    {
        var arrBytes = (array.Count - 1) / 8 + 1;
        _regionBytes[_region] += arrBytes;
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

    public void PrintStatistics()
    {
        foreach (var (region, bytes) in _regionBytes)
        {
            Console.WriteLine($"{region}: {bytes} ({Math.Round(bytes*100.0/Count)}%))");
        }
    }
}