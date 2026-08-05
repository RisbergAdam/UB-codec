using System.Runtime.Intrinsics;
using UBCodec.Core.Utils;

namespace UBCodec.Core.Encoder.Sampling;

public class SubpixelSampler
{
    public static byte[,] Crop(byte[,] input, float x, float y, int size)
    {
        var width = input.GetLength(0);
        var height = input.GetLength(1);
        
        int intX = (int)MathF.Floor(x);
        int intY = (int)MathF.Floor(y);

        float fracX = x - intX;
        float fracY = y - intY;
        
        int phaseX = (int)MathF.Round(fracX * 4.0f) & 3;
        int phaseY = (int)MathF.Round(fracY * 4.0f) & 3;
        
        // Helper lambda for frame edge-clamping (sample extension)
        int Clamp(int v, int max) => Math.Clamp(v, 0, max - 1);
            
        if (phaseX == 0 && phaseY == 0)
        {
            var fastResult = new byte[size, size];
            for (int r = 0; r < size; r++)
            {
                int clampedY = Clamp(intY + r, height);
                for (int c = 0; c < size; c++)
                {
                    int sampleX = Clamp(intX + c, width);
                    fastResult[c, r] = input[sampleX, clampedY];
                }
            }
            return fastResult;
        }

        // Adjust integer base if rounding wrapped around (i.e. frac rounded to 1.0)
        intX += (int)MathF.Round(fracX * 4.0f) >> 2;
        intY += (int)MathF.Round(fracY * 4.0f) >> 2;

        // 8-tap DCT-IF filter coefficients for 1/4-pel phases (normalized to sum to 64)
        var filter8Tap = new int[][]
        {
            [0,  0,   0, 64,  0,  0,  0,  0], // Phase 0 (Integer)
            [-1,  4, -10, 58, 17, -5,  1,  0], // Phase 1 (1/4-pel)
            [-1,  4, -11, 40, 40,-11,  4, -1], // Phase 2 (1/2-pel)
            [0,  1,  -5, 17, 58,-10,  4, -1] // Phase 3 (3/4-pel)
        };

        var coeffX = filter8Tap[phaseX];
        var coeffY = filter8Tap[phaseY];
        
        // Intermediate buffer for 1D horizontal pass: needs 7 extra rows for vertical 8-tap span
        var intermediate = new int[size + 7, size];

        // 2. Horizontal Pass (Filter across rows)
        for (int r = -3; r < size + 4; r++)
        {
            int clampedY = Clamp(intY + r, height);
            int interRowIdx = r + 3;

            for (int c = 0; c < size; c++)
            {
                int sum = 0;
                for (int k = 0; k < 8; k++)
                {
                    int sampleX = Clamp(intX + c - 3 + k, width);
                    sum += coeffX[k] * input[sampleX, clampedY];
                }
                // Keep internal precision (shift by 2, leaving factor of 16 for vertical pass)
                intermediate[interRowIdx, c] = sum >> 2; 
            }
        }

        // 3. Vertical Pass (Filter down columns)
        var result = new byte[size, size];
        
        for (int r = 0; r < size; r++)
        for (int c = 0; c < size; c++)
        {
            int sum = 0;
            for (int k = 0; k < 8; k++)
            {
                sum += coeffY[k] * intermediate[r + k, c];
            }

            // Divide out the combined filter gain (64 * 16 = 1024 = 2^10) with rounding
            int val = (sum + 512) >> 10;
            result[c, r] = MathUtils.BClamp(val);
        }

        return result;
    }

    public static PlanarImage Crop(PlanarImage input, float x, float y, int size)
    {
        var xd = x / input.ChromaDownsample;
        var yd = y / input.ChromaDownsample;
        var sd = size / input.ChromaDownsample;
        
        var image = PlanarImage.FromSize(size, size, input.ChromaDownsample);
        image.LBuffer = Crop(input.LBuffer, x, y, size);
        image.C1Buffer = Crop(input.C1Buffer, xd, yd, sd);
        image.C2Buffer = Crop(input.C2Buffer, xd, yd, sd);
        return image;
    }
}