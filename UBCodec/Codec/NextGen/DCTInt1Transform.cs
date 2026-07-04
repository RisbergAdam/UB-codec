namespace UBCodec.Codec.NextGen;

public class DctInt1Transform : ITransform
{
    private static int[,] M_32 =
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

    private static int[,] M_826 =
    {
        { 292, 405, 382, 343, 292, 229, 158, 81 },
        { 292, 343, 158, -81, -292, -405, -382, -229 },
        { 292, 229, -158, -405, -292, 81, 382, 343 },
        { 292, 81, -382, -229, 292, 343, -158, -405 },
        { 292, -81, -382, 229, 292, -343, -158, 405 },
        { 292, -229, -158, 405, -292, -81, 382, -343 },
        { 292, -343, 158, 81, -292, 405, -382, 229 },
        { 292, -405, 382, -343, 292, -229, 158, -81 },
    };

    private int[,] M = M_32;
    
    private int[,] MT = new int[8, 8];

    private int QFactor = 32;
    
    private int[,] _workmem = new int[8, 8];

    public DctInt1Transform()
    {
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            MT[x, y] = M[y, x];
        }
    }

    public int[,] Transform(int[,] input)
    {
        var blockSize = input.GetLength(0);
        var binput = new byte[blockSize, blockSize];
        
        for (var y = 0; y < blockSize; y++)
        for (var x = 0; x < blockSize; x++)
        {
            binput[x, y] = (byte) input[x, y];
        }
        
        var output = new int[8, 8];
        TransformForward(blockSize, binput, output);
        return output;
    }

    public void TransformForward(int blockSize, byte[,] input, int[,] output)
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
                _workmem[x, y] += M[i, y] * (input[x + 8 * xb, i + 8 * yb] - 127);
                // _workmem[x, y] += M[i, y] * (input[x + 8 * xb, i + 8 * yb]);
            }
            
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            for (var i = 0; i < 8; i++)
            {
                output[x + 8*xb, y + 8*yb] += _workmem[i, y] * M[i, x];
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
    
    public void TransformInverse(int blockSize, int[,] input, byte[,] output)
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
                _workmem[x, y] += M[y, i] * input[x + 8 * xb, i + 8 * yb];
            }
            
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            for (var i = 0; i < 8; i++)
            {
                output[x + 8*xb, y + 8*yb] += (byte) (_workmem[i, y] * M[x, i] / (QFactor * QFactor) + 15);
            }
            
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                // output[x + 8*xb, y + 8*yb] /= QFactor * QFactor;
                output[x + 8 * xb, y + 8 * yb] += 7;
            }
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