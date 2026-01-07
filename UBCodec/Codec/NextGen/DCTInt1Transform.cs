namespace UBCodec.Codec.NextGen;

public class DCTInt1Transform : ITransform
{
    private int[,] M =
    {
        { 11, 16, 15, 13, 11, 9, 6, 3 },
        { 11, 13, 6, -3, -11, -16, -15, -9 },
        { 11, 9, -6, -16, -11, 3, 15, 13 },
        { 11, 3, -15, -9, 11, 13, -6, -16 },
        { 11, -3, -15, 9, 11, -13, -6, 16 },
        { 11, -9, -6, 16, -11, -3, 15, -13 },
        { 11, -13, 6, 3, -11, 16, -15, 9 },
        { 11, -16, 15, -13, 11, -9, 6, -3 },
    };

    private int QFactor = 32;
    
    private int[,] _workmem = new int[8, 8];

    public int[,] Transform(int[,] input, bool inverse)
    {
        var blockSize = input.GetLength(0);
        var binput = new byte[blockSize, blockSize];
        
        for (var y = 0; y < blockSize; y++)
        for (var x = 0; x < blockSize; x++)
        {
            binput[x, y] = (byte) input[x, y];
        }
        
        var output = new int[8, 8];
        Transform(blockSize, binput, inverse, output);
        return output;
    }
    
    public void Transform(int blockSize, byte[,] input, bool inverse, int[,] output)
    {
        var subBlocks = blockSize / 8;
        
        for (var yb = 0; yb < subBlocks; yb++)
        for (var xb = 0; xb < subBlocks; xb++)
        {
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                output[x + 8 * xb, y + 8 * yb] = 0;
                _workmem[x, y] = 0;
            }
            
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            for (var i = 0; i < 8; i++)
            {
                if (inverse)
                {
                    _workmem[x, y] += M[y, i] * input[x + 8 * xb, i + 8 * yb];
                }
                else
                {
                    _workmem[x, y] += M[i, y] * (input[x + 8 * xb, i + 8 * yb] - 127);
                }
            }
            
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            for (var i = 0; i < 8; i++)
            {
                if (inverse)
                {
                    output[x + 8*xb, y + 8*yb] += _workmem[i, y] * M[x, i];
                }
                else
                {
                    output[x + 8*xb, y + 8*yb] += _workmem[i, y] * M[i, x];   
                }
            }

            var maxCoef = 0;
            
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                maxCoef = Math.Max(maxCoef, output[x + 8 * xb, y + 8 * yb]);
                output[x + 8*xb, y + 8*yb] /= QFactor * QFactor;
            }
            
            // Console.WriteLine(maxCoef);
        }
    }

    public static int[,] CreateMatrix(int qFactor)
    {
        var M = new double[8, 8];
        
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            if (y == 0) M[x, y] = 1.0 / Math.Sqrt(8.0);
            else M[x, y] = Math.Sqrt(2.0 / 8.0) * Math.Cos((2*x+1) * y * Math.PI / 8.0 / 2.0);
        }
        
        var MQ = new int[8, 8];
        
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            MQ[x, y] = (int) Math.Round(qFactor * M[x, y]);
        }
        
        return MQ;
    }
}