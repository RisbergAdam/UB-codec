namespace UBCodec.Codec;

public interface ITransform
{

    int[,] Transform(int[,] input, bool inverse);

}

