using System.Collections;
using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder;

public interface ICoder
{
    public void Encode(int blockSize, int[,] input, BitList output);
    
    public void Decode(int blockSize, BitList input, int[,] output);
}