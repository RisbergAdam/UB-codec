using System.Collections;
using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder;

public class GolombRiceCoder : ICoder
{
    public int GolombM { get; set; } = 64; // GR parameter for coefficient values
    public int GolombZM { get; set; } = 64; // GR parameter for zero-run lengths

    // ── coding modes (2-bit header) ───────────────────────────────────
    // 00 = all-zero, 01 = normal, 10 = median
    private enum CodingMode { AllZero = 0, Normal = 1, Median = 2 }

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
        
        int zeroes = 0;
        var tuples = new List<(int, int)>();
        
        foreach (int coef in flat)
        {
            if (coef == 0)
            {
                zeroes++;
            }
            else
            {
                Console.Write($"({zeroes}, {coef}) ");
                tuples.Add((zeroes, coef));
                zeroes = 0;
            }
        }

        // ── mode detection ────────────────────────────────────────────
        CodingMode mode;
        int modeValue = 0;

        if (tuples.Count == 0)
        {
            mode = CodingMode.AllZero;
        }
        else if (TryDetectMode(tuples, out modeValue))
        {
            mode = CodingMode.Median;
        }
        else
        {
            mode = CodingMode.Normal;
        }

        var bits = new BitList();
        // Precompute log2 parameters once
        int K_RUN = (int)Math.Log2(GolombZM);
        int K_VAL = (int)Math.Log2(GolombM);

        // ── 2-bit mode header ──────────────────────────────────────
        bits.AddBit(((int)mode >> 1) & 1);
        bits.AddBit(((int)mode >> 0) & 1);

        switch (mode)
        {
            case CodingMode.AllZero:
                Console.WriteLine("All-zero mode");
                break;

            case CodingMode.Median:
                var M = 16;
                Console.WriteLine($"Median mode ({modeValue}, {zeroes})");
                WriteSignedGolombRice(bits, modeValue, M);
                WriteGolombRice(bits, tuples.Count, M);
                WriteGolombRice(bits, zeroes, M);
                break;

            case CodingMode.Normal:
                Console.WriteLine("Normal mode");
                foreach (var (z, coef) in tuples)
                {
                    WriteGolombRice(bits, z, K_RUN);
                    WriteSignedGolombRice(bits, coef, K_VAL);
                }
                WriteGolombRice(bits, zeroes, K_RUN);
                break;
        }

        output.WriteBitArray(bits.GetArray());
    }

    // ── mode detection ─────────────────────────────────────────────────

    /// <summary>
    /// Detect whether to use median-mode. Mode = most frequent non-zero coefficient.
    /// Returns true if sum of absolute deltas ≤ 10 AND all zero-runs are 0
    /// (coefficients are contiguous with no zeros between them).
    /// </summary>
    private static bool TryDetectMode(List<(int z, int coeff)> tuples, out int modeValue)
    {
        modeValue = 0;

        if (tuples.Count == 0)
            return false;

        // median-mode only valid when no zeros between non-zero coeffs
        foreach (var (z, _) in tuples)
        {
            if (z != 0)
                return false;
        }

        var histogram = new Dictionary<int, int>();
        foreach (var (_, coeff) in tuples)
        {
            histogram.TryGetValue(coeff, out int c);
            histogram[coeff] = c + 1;
        }

        // most frequent non-zero coefficient
        int bestCount = 0;
        foreach (var kv in histogram)
        {
            if (kv.Value > bestCount)
            {
                bestCount = kv.Value;
                modeValue = kv.Key;
            }
        }

        // sum of absolute deltas from mode
        int error = 0;
        foreach (var (_, coeff) in tuples)
            error += Math.Abs(coeff - modeValue);

        return error <= 10;
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

        int modeBits = (bits[pos] ? 2 : 0) | (bits[pos + 1] ? 1 : 0);
        pos += 2;
        var mode = (CodingMode)modeBits;

        switch (mode)
        {
            case CodingMode.AllZero:
                // nothing to decode — flat already all zeros
                break;

            case CodingMode.Median:
            {
                int modeValue = UnmapSign(ReadGR(bits, ref pos, K_VAL));
                int count = ReadGR(bits, ref pos, K_RUN);

                for (int i = 0; i < count; i++)
                    flat[decoded++] = modeValue;

                int trailingZeroes = ReadGR(bits, ref pos, K_RUN);
                for (int i = 0; i < trailingZeroes && decoded < total; i++)
                    flat[decoded++] = 0;
                break;
            }

            case CodingMode.Normal:
                while (decoded < total)
                {
                    int run = ReadGR(bits, ref pos, K_RUN);

                    for (int i = 0; i < run && decoded < total; i++)
                        flat[decoded++] = 0;

                    if (decoded >= total)
                        break;

                    int mapped = ReadGR(bits, ref pos, K_VAL);
                    flat[decoded++] = UnmapSign(mapped);
                }
                break;
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