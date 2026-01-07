namespace UBCodec.Codec.NextGen;

public interface ITransform
{
    void Transform(int blockSize, byte[,] input, bool inverse, int[,] output);
}