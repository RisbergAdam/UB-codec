using SkiaSharp;

namespace UBCodec.Codec;

public class ArrayUtils
{
    public static int[,] ToArray(SKBitmap input)
    {
        var output = new int[8, 8];
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            output[x, y] = input.GetPixel(x, y).Red - 127;
        return output;
    }

    public static int[] GetRow(int[,] input, int rowIx)
    {
        var row = new int[8];
        for (int i = 0; i < row.Length; i++)
            row[i] = input[i, rowIx];
        return row;
    }

    public static int[,] Transpose(int[,] input)
    {
        var output = new int[8, 8];
        for (var y = 0; y < 8; y++)
        for (var x = 0; x < 8; x++)
            output[x, y] = input[y, x];
        return output;
    }

    public static int[] Add(int[] r1, int[] r2)
    {
        int[] row = new int[r1.Length];
        for (int i = 0; i < row.Length; i++)
            row[i] = r1[i] + r2[i];
        return row;
    }
    
    public static int[] Sub(int[] r1, int[] r2)
    {
        int[] row = new int[r1.Length];
        for (int i = 0; i < row.Length; i++)
            row[i] = r1[i] - r2[i];
        return row;
    }
    
    public static int[] Div(int[] r1, int f)
    {
        int[] row = new int[r1.Length];
        for (int i = 0; i < row.Length; i++)
            row[i] = r1[i] / f;
        return row;
    }
}