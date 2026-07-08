using System.Collections;
using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder;

public class GolombRiceCoder : ICoder
{
    public bool RLE { get; set; } = true;
    public int RLEMax { get; set; } = 65536;
    public bool Golomb { get; set; } = true;
    public int GolombM { get; set; } = 64;
    public bool ZigZag { get; set; } = true;

    static readonly int[,] ZigZagIx =
    {
        { 0, 1, 5, 6, 14, 15, 27, 28 },
        { 2, 4, 7, 13, 16, 26, 29, 42 },
        { 3, 8, 12, 17, 25, 30, 41, 43 },
        { 9, 11, 18, 24, 31, 40, 44, 53 },
        { 10, 19, 23, 32, 39, 45, 52, 54 },
        { 20, 22, 33, 38, 46, 51, 55, 60 },
        { 21, 34, 37, 47, 50, 56, 59, 61 },
        { 35, 36, 48, 49, 57, 58, 62, 63 },
    };

    static readonly int[,] InvZigZagIx =
    {
        { 0, 10, 33, 27, 28, 43, 23, 46 },
        { 8, 3, 26, 34, 21, 50, 31, 39 },
        { 1, 4, 19, 41, 14, 57, 38, 47 },
        { 2, 11, 12, 48, 7, 58, 45, 54 },
        { 9, 18, 5, 56, 15, 51, 52, 61 },
        { 16, 25, 6, 49, 22, 44, 59, 62 },
        { 24, 32, 13, 42, 29, 37, 60, 55 },
        { 17, 40, 20, 35, 36, 30, 53, 63 },
    };

    public void Encode(int blockSize, int[,] input, ByteStreamWriter output)
    {
        var flat = new int[blockSize * blockSize];
        for (int y = 0; y < blockSize; y++)
        for (int x = 0; x < blockSize; x++)
            flat[y * blockSize + x] = input[x, y];

        if (ZigZag) flat = ZigZagReorder(flat, blockSize);

        var encoded = _GolombRiceEncode(_RLEEncode(flat));
        output.WriteBitArray(encoded.GetArray());
    }

    public void Decode(int blockSize, ByteStreamReader input, int[,] output)
    {
        var flat = _RLEDecode(_GolombRiceDecode(input.ReadBitArray()));

        if (ZigZag) flat = InverseZigZagReorder(flat, blockSize);

        for (int y = 0; y < blockSize; y++)
        for (int x = 0; x < blockSize; x++)
            output[x, y] = flat[y * blockSize + x];
    }

    static int[] ZigZagReorder(int[] input, int blockSize) => _Reorder(input, blockSize, ZigZagIx);
    static int[] InverseZigZagReorder(int[] input, int blockSize) => _Reorder(input, blockSize, InvZigZagIx);

    static int[] _Reorder(int[] input, int blockSize, int[,] table)
    {
        var output = new int[input.Length];
        var subBlocks = blockSize / 8;
        for (var yb = 0; yb < subBlocks; yb++)
        for (var xb = 0; xb < subBlocks; xb++)
        {
            var off = (yb * subBlocks + xb) * 64;
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
                output[off + table[x, y]] = input[off + y * 8 + x];
        }
        return output;
    }
    
    private BitList _GolombRiceEncode(int[] input)
    {
        var bits = new BitList();
        int K = (int) Math.Log2(GolombM);
        
        foreach (var v in input)
        {
            var value = v >= 0 ? v * 2 : -v * 2 - 1;
            var Q = value >> K;
            var R = value & (GolombM - 1);
            for (var i = 0; i < Q; i++) bits.AddBit(1);
            bits.AddBit(0);
            for (var i = 0; i < K; i++) bits.AddBit((R & (1 << i)) != 0 ? 1 : 0);
        }
        
        return bits;
    }

    private int[] _GolombRiceDecode(BitArray input)
    {
        var output = new List<int>();
        int K = (int) Math.Log2(GolombM);

        bool decodeQ = true;
        var Q = 0;
        var R = 0;
        var bitsR = 0;

        foreach (bool v in input)
        {
            if (decodeQ)
            {
                if (v) Q++;
                else
                {
                    R = 0;
                    bitsR = 0;
                    decodeQ = false;
                }
            }
            else
            {
                if (v) R |= (1 << bitsR);
                bitsR++;
                if (bitsR == K)
                {
                    var value = Q * GolombM + R;
                    value = (value % 2 == 0) ? value / 2 : (value + 1) / -2;
                    output.Add(value);
                    Q = 0;
                    decodeQ = true;
                }
            }
        }

        return output.ToArray();
    }

    private int[] _RLEEncode(int[] input)
    {
        if (!RLE) return input;
        
        var output = new List<int>();

        var symbol = input[0];
        var count = 0;

        foreach (var v in input)
        {
            if (v == symbol && count < RLEMax) count++;
            else
            {
                output.Add(count);
                output.Add(symbol);
                symbol = v;
                count = 1;
            }
        }
        
        output.Add(count);
        output.Add(symbol);
        return output.ToArray();
    }
    
    private int[] _RLEDecode(int[] input)
    {
        if (!RLE) return input;
        
        var output = new List<int>();

        for (var i = 0; i < input.Length; i += 2)
        {
            for (var r = 0; r < input[i]; r++) output.Add(input[i+1]);
        }
        
        return output.ToArray();
    }
}