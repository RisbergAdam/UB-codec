namespace UBCodec.Codec.NextGen;

public interface ITransform
{
    void TransformForward(int blockSize, byte[,] input, int[,] output);
    
    void TransformInverse(int blockSize, int[,] input, byte[,] output);
}