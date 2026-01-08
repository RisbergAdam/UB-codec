using System.Collections;

namespace UBCodec.Codec.NextGen;

public interface ICoder
{
    public void Encode(int blockSize, int[,] input, ByteStreamWriter output);
    
    public void Decode(int blockSize, ByteStreamReader input, int[,] output);
}