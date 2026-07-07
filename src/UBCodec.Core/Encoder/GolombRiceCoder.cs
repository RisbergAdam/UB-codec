using System.Collections;
using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder;

public class GolombRiceCoder : ICoder
{
    public bool RLE { get; set; } = true;
    
    public int RLEMax { get; set; } = 65536;
    
    public bool Golomb { get; set; } = true;
    
    public int GolombM { get; set; } = 64;
    
    public void Encode(int blockSize, int[,] input, ByteStreamWriter output)
    {
        var input1d = new int[blockSize * blockSize];

        for (int y = 0; y < blockSize; y++)
        for (int x = 0; x < blockSize; x++)
        {
            input1d[y * blockSize + x] = input[x, y];
        }
        
        var encoded = _GolombRiceEncode(_RLEEncode(input1d));
        output.WriteBitArray(encoded.GetArray());
    }

    public void Decode(int blockSize, ByteStreamReader input, int[,] output)
    {
        var decoded = _RLEDecode(_GolombRiceDecode(input.ReadBitArray()));
        
        for (int y = 0; y < blockSize; y++)
        for (int x = 0; x < blockSize; x++)
        {
            output[x, y] = decoded[y * blockSize + x];
        }
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