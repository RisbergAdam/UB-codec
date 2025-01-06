namespace UBCodec.Codec;

public class DCTReferenceTransform : ITransform
{
    private double[,] M = new double[8, 8];
    
    private double[,] MT = new double[8, 8];

    public DCTReferenceTransform()
    {
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            if (y == 0) M[x, y] = (1.0 / Math.Sqrt(8.0));
            else M[x, y] = (Math.Sqrt(2.0 / 8.0) * Math.Cos((2*x+1) * y * Math.PI / 8.0 / 2.0));
        }
        
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
        {
            MT[x, y] = M[y, x];
        }
    }

    public int[,] Transform(int[,] input, bool inverse)
    {
        double[,] m = M, mt = MT;
        
        if (inverse)
        {
            (m, mt) = (mt, m);
        }
        
        double[,] output = new double[8, 8];
        double[,] temp = new double[8, 8];
        
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
        
        var outputInt = new int[8, 8];
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            outputInt[x, y] = (int)Math.Round(output[x, y]);
        
        return outputInt;
    }
}