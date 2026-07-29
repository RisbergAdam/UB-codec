using CommunityToolkit.HighPerformance;
using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder.ImageTransform;

public class DCTMatrix : ITransform8X8
{
    private static readonly int[,] M =
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

    private const int Q = 826;
    
    private readonly int[,] _tmp = new int[8, 8];

    public void Forward(Span2D<byte> input, Span2D<int> output)
    {
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int s = 0;
            for (int i = 0; i < 8; i++)
                s += M[i, y] * (input[x, i] - 127);
            _tmp[x, y] = s;
        }

        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int s = 0;
            for (int i = 0; i < 8; i++)
                s += _tmp[i, y] * M[i, x];
            output[x, y] = s / (Q * Q);
        }
    }

    public void Inverse(Span2D<int> input, Span2D<byte> output)
    {
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            int s = 0;
            for (int i = 0; i < 8; i++)
                s += M[y, i] * input[x, i];
            _tmp[x, y] = s;
        }

        long norm = (long)Q * Q;
        long round = norm / 2;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            long s = 0;
            for (int i = 0; i < 8; i++)
            {
                s += (long)_tmp[i, y] * M[x, i];
            }

            var v = (int)(s >= 0 ? (s + round) / norm : -((-s + round) / norm));
            output[x, y] = MathUtils.BClamp(v + 127);
        }
    }
}