namespace RiskyCodec.Codec;

public class DCTInteger1Transform : ITransform
{
    private int[,] M = new int[8, 8];
    
    private int[,] MT = new int[8, 8];
    
    public int QFactor { get; set; }

    public DCTInteger1Transform(int qFactor)
    {
        QFactor = qFactor;
        
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            if (y == 0) M[x, y] = (int)(QFactor * 1.0 / Math.Sqrt(8.0));
            else M[x, y] = (int)(QFactor * Math.Sqrt(2.0 / 8.0) * Math.Cos((2*x+1) * y * Math.PI / 8.0 / 2.0));
        }
        
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            MT[x, y] = M[y, x];
        }
    }
    
    public int[,] Transform(int[,] input, bool inverse)
    {
        int[,] m = M, mt = MT;
        
        if (inverse)
        {
            (m, mt) = (mt, m);
        }
        
        var output = new int[8, 8];
        var temp = new int[8, 8];
        
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        for (var i = 0; i < 8; i++)
        {
            temp[x, y] += (m[i, y] * input[x, i]);
        }
        
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        for (var i = 0; i < 8; i++)
        {
            output[x, y] += (temp[i, y] * mt[x, i]);
        }
        
        var outputScaled = new int[8, 8];
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            outputScaled[x, y] = output[x, y] / QFactor / QFactor;
        
        return outputScaled;
    }
}