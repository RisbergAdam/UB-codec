using System.Collections;

namespace UBCodec.Core.Encoder;

public interface ICoder
{
    public void Encode(int blockSize, int[,] input, ByteStreamWriter output);
    
    public void Decode(int blockSize, ByteStreamReader input, int[,] output);
}