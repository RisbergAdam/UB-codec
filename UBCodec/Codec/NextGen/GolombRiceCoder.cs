using System.Collections;
using SkiaSharp;

namespace UBCodec.Codec.NextGen;

public class GolombRiceCoder : ICoder
{
    public bool RLE { get; set; } = true;
    
    public int RLEMax { get; set; } = 65536;
    
    public bool Golomb { get; set; } = true;
    
    public int GolombM { get; set; } = 64;
    
    public bool ZigZag { get; set; } = true;
    
    public void Encode(int blockSize, int[,] input, ByteStreamWriter output)
    {
        var input1d = new int[blockSize * blockSize];

        for (int y = 0; y < blockSize; y++)
        for (int x = 0; x < blockSize; x++)
        {
            input1d[y * blockSize + x] = input[x, y];
        }
        
        var encoded = _GolombRiceEncode(_RLEEncode(input1d));
        output
            // .WriteUInt16((ushort)encoded.Count)
            .WriteBitArray(encoded.GetArray());
    }

    public void Decode(int blockSize, BitArray input, int[,] output)
    {
        var decoded = _RLEDecode(_GolombRiceDecode(input));
        
        for (int y = 0; y < blockSize; y++)
        for (int x = 0; x < blockSize; x++)
        {
            output[x, y] = decoded[y * blockSize + x];
        }
    }
    
    private SKBitmap _ZigZagScan(SKBitmap input, bool inverse = false)
    {
        if (!ZigZag) return input;
    
        var output = new SKBitmap(input.Info);

        int[,] zigZagIx =
        {
            { 0, 1, 5, 6, 14, 15, 27, 28 },
            { 2, 4, 7, 13, 16, 26, 29, 42 },
            { 3, 8, 12, 17, 25, 30, 41, 43 },
            { 9, 11, 18, 24, 31, 40, 44, 53 },
            { 10, 19, 23, 32, 39, 45, 52, 54 },
            { 20, 22, 33, 38, 46, 51, 55, 60 },
            { 21, 34, 37, 47, 50, 56, 59, 61 },
            { 35, 36, 48, 49, 57, 58, 62, 63 }
        };

        int[,] invZigZagIx =
        {
            { 0, 10, 33, 27, 28, 43, 23, 46 },
            { 8, 3, 26, 34, 21, 50, 31, 39 },
            { 1, 4, 19, 41, 14, 57, 38, 47 },
            { 2, 11, 12, 48, 7, 58, 45, 54 },
            { 9, 18, 5, 56, 15, 51, 52, 61 },
            { 16, 25, 6, 49, 22, 44, 59, 62 },
            { 24, 32, 13, 42, 29, 37, 60, 55 },
            { 17, 40, 20, 35, 36, 30, 53, 63 }
        };
    
        for (var by = 0; by < input.Height / 8; by++)
        for (var bx = 0; bx < input.Width / 8; bx++)
        {
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var p = input.GetPixel(bx * 8 + x, by * 8 + y);
                var zIx = inverse ? invZigZagIx[x, y] : zigZagIx[x, y];;
                output.SetPixel(bx * 8 + zIx % 8, by * 8 + zIx / 8, p);
            }
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
        if (!Golomb)
        {
            var output = new int[input.Length / 8];
            input.CopyTo(output, 0);
            return output;
        } else {
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