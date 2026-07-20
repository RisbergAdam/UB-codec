using System.Collections;
using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder;

public class GolombRiceCoder : ICoder
{
    public int GolombM { get; set; } = 64; // GR parameter for coefficient values
    public int GolombZM { get; set; } = 64; // GR parameter for zero-run lengths

    public void Encode(int blockSize, int[,] input, ByteStreamWriter output)
    {
        int total = blockSize * blockSize;
        var flat = new int[total];
        int ix = 0;

        // Block-interleaved scan: for each (x,y) within an 8x8 tile,
        // visit all sub-blocks before moving to the next (x,y)
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            for (int yb = 0; yb < blockSize / 8; yb++)
            for (int xb = 0; xb < blockSize / 8; xb++)
            {
                flat[ix++] = input[xb * 8 + x, yb * 8 + y];
            }
        }

        var bits = new BitList();
        int zeroes = 0;

        // Precompute log2 parameters once
        int K_RUN = (int)Math.Log2(GolombZM);
        int K_VAL = (int)Math.Log2(GolombM);

        foreach (int coef in flat)
        {
            if (coef == 0)
            {
                zeroes++;
            }
            else
            {
                WriteGolombRice(bits, zeroes, K_RUN);
                WriteSignedGolombRice(bits, coef, K_VAL);
                zeroes = 0;
            }
        }

        // Mandatory final run (covers trailing zeros, is 0 if block ended with a non-zero)
        WriteGolombRice(bits, zeroes, K_RUN);

        output.WriteBitArray(bits.GetArray());
    }

    // ── helpers ────────────────────────────────────────────────────────

    /// <summary>Encode a non-negative integer with Golomb–Rice.</summary>
    private static void WriteGolombRice(BitList bits, int value, int K)
    {
        int Q = value >> K; // quotient
        int R = value & ((1 << K) - 1); // remainder

        // unary quotient
        for (int i = 0; i < Q; i++) bits.AddBit(1);
        bits.AddBit(0); // delimiter

        // remainder in K bits, MSB-first  [FIX #1]
        for (int i = K - 1; i >= 0; i--)
            bits.AddBit((R & (1 << i)) != 0 ? 1 : 0);
    }

    /// <summary>Encode a signed integer with sign-mapped Golomb–Rice.</summary>
    private static void WriteSignedGolombRice(BitList bits, int value, int K)
    {
        // Signed → unsigned mapping:  0→0, +1→2, −1→1, +2→4, −2→3, …
        // Guard against overflow  [FIX #3]
        int abs = value >= 0 ? value : -value; // |value|
        int mapped = value >= 0 ? (abs * 2) : (abs * 2 - 1);

        WriteGolombRice(bits, mapped, K);
    }

    public void Decode(int blockSize, ByteStreamReader input, int[,] output)
    {
        var bits = input.ReadBitArray();
        int pos = 0;
        int total = blockSize * blockSize;
        var flat = new int[total];
        int decoded = 0;

        int K_RUN = (int)Math.Log2(GolombZM);
        int K_VAL = (int)Math.Log2(GolombM);

        while (decoded < total)
        {
            // Decode a zero-run length (raw integer, no sign unmapping)
            int run = ReadGR(bits, ref pos, K_RUN);

            for (int i = 0; i < run && decoded < total; i++)
                flat[decoded++] = 0;

            if (decoded >= total)
                break;

            // Decode a signed coefficient
            int mapped = ReadGR(bits, ref pos, K_VAL);
            flat[decoded++] = UnmapSign(mapped);
        }

        // ── inverse block-interleaved scan ─────────────────────────────────
        int ix = 0;
        for (int y = 0; y < 8; y++)
        for (int x = 0; x < 8; x++)
        {
            for (int yb = 0; yb < blockSize / 8; yb++)
            for (int xb = 0; xb < blockSize / 8; xb++)
            {
                output[xb * 8 + x, yb * 8 + y] = flat[ix++];
            }
        }
    }

// ── bit-level helpers ─────────────────────────────────────────────────

    /// <summary>Decode one non-negative Golomb–Rice codeword from the BitArray.</summary>
    private static int ReadGR(BitArray bits, ref int pos, int K)
    {
        // ---- unary quotient: count 1s until the 0 delimiter ----
        int Q = 0;
        while (pos < bits.Length && bits[pos])
        {
            Q++;
            pos++;
        }

        pos++; // skip the delimiter 0

        // ---- remainder: K bits, MSB-first ----
        int R = 0;
        for (int i = K - 1; i >= 0; i--)
        {
            if (pos < bits.Length && bits[pos])
                R |= (1 << i);
            pos++;
        }

        return (Q << K) | R;
    }

    /// <summary>Reverse the sign→unsigned mapping.</summary>
    private static int UnmapSign(int mapped)
    {
        if ((mapped & 1) == 0) // even → non-negative
            return mapped >> 1;
        else // odd → negative
            return -((mapped + 1) >> 1);
    }
}