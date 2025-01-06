using System.Collections;

namespace RiskyCodec.Codec;

public class BitList
{
    private BitArray _array = new BitArray(1);

    private int _bitsUsed = 0;

    public BitArray Get()
    {
        var output = new BitArray(_array);
        output.Length = _bitsUsed;
        return output;
    }

    public void AddBit(int v)
    {
        _EnsureCapacity(1);
        _array.Set(_bitsUsed, v != 0);
        _bitsUsed += 1;
    }

    private void _EnsureCapacity(int count)
    {
        while (_bitsUsed + count > _array.Length) _array.Length *= 2;
    }

}