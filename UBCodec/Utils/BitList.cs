using System.Collections;

namespace UBCodec.Codec;

public class BitList
{
    private BitArray _array = new(1);

    private int _bitsUsed = 0;

    public BitArray GetArray()
    {
        var output = new BitArray(_array);
        output.Length = _bitsUsed;
        return output;
    }
    
    public int Count => _bitsUsed;

    public void AddBit(int v)
    {
        _EnsureCapacity(1);
        _array.Set(_bitsUsed, v != 0);
        _bitsUsed += 1;
    }

    public void AddBits(BitArray bits)
    {
        _EnsureCapacity(bits.Count);
        for (int ix = 0; ix < bits.Count; ix++)
        {
            _array.Set(_bitsUsed + ix, bits.Get(ix));
        }
        _bitsUsed += bits.Count;
    }

    public void AddInt(int bits, int value)
    {
        for (int i = 0; i < bits; i++)
        {
            AddBit(value & (1 << i));   
        }
    }

    private void _EnsureCapacity(int count)
    {
        while (_bitsUsed + count > _array.Length) _array.Length *= 2;
    }

}