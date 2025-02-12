namespace UBCodec.Codec.NextGen;

public class BlockMemory(int size)
{
    private long[] _memory = new long[size];

    public long Read(int address)
    {
        return _memory[address];
    }

    public void Write(int address, long value)
    {
        _memory[address] = value;
    }

}