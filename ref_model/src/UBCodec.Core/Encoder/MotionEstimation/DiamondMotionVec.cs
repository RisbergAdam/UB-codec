using System.Numerics;
using CommunityToolkit.HighPerformance;

namespace UBCodec.Core.Encoder.MotionEstimation;

public class DiamondMotionVec : IMotionEstimator
{
    // Downsample factor for the coarse pass of the hierarchical search.
    private const int DownsampleFactor = 4;

    // Diamond search patterns, expressed as (dx, dy) offsets applied to the
    // current search centre. LDSP (Large Diamond Search Pattern) uses a step of
    // 2; SDSP (Small Diamond Search Pattern) uses a step of 1 for the final
    // refinement. The centre (0, 0) is included so we can detect convergence.
    private static readonly (int Dx, int Dy)[] LargeDiamond =
    {
        ( 0,  0),
        (-2,  0), ( 2,  0), ( 0, -2), ( 0,  2),
        (-1, -1), (-1,  1), ( 1, -1), ( 1,  1),
    };

    private static readonly (int Dx, int Dy)[] SmallDiamond =
    {
        ( 0,  0),
        (-1,  0), ( 1,  0), ( 0, -1), ( 0,  1),
    };

    public EstimatedMotion Estimate(Span2D<byte> source, Span2D<byte> template)
    {
        // The buffers are byte[width, height] indexed [x, y] with x = first index.
        // Span2D maps span[a, b] == array[a, b], so the FIRST span index is x.
        // Keep the same convention as IntegerMotionRef: template(x, y) vs source(x + dx, y + dy).
        int padX = source.Height - template.Height; // search range for the x (first) index
        int padY = source.Width - template.Width;   // search range for the y (second) index
        long n = (long)template.Width * template.Height;

        if (padX < 0 || padY < 0)
            return default;

        long errorBest = long.MaxValue;
        int xBest = 0;
        int yBest = 0;

        // Cache of already-computed SADs for each evaluated full-res offset; overlapping
        // points between diamond iterations and across seeds are not recomputed.
        var cache = new Dictionary<(int X, int Y), long>();

        // --- Coarse pass: find the rough global-minimum basin on downsampled images.
        // Downsampling averages out the high-frequency "needles" that make the SAD
        // surface multi-modal, so the diamond lands in the correct basin instead of
        // a spurious local minimum of the full-resolution surface.
        int coarseX = 0;
        int coarseY = 0;
        bool haveCoarse = false;

        var coarseSource = Downsample(source);
        var coarseTemplate = Downsample(template);
        int coarsePadX = coarseSource.Height - coarseTemplate.Height;
        int coarsePadY = coarseSource.Width - coarseTemplate.Width;

        if (coarsePadX >= 0 && coarsePadY >= 0)
        {
            var coarseCache = new Dictionary<(int X, int Y), long>();
            long coarseBest = long.MaxValue;
            RunDiamond(coarseSource, coarseTemplate, coarsePadX, coarsePadY,
                       coarsePadX / 2, coarsePadY / 2, coarseCache,
                       ref coarseBest, ref coarseX, ref coarseY);
            haveCoarse = true;
        }

        // --- Fine pass: refine at full resolution from the coarse location.
        int f = DownsampleFactor;

        if (haveCoarse)
        {
            // Seed a small cluster around the coarse estimate (offset by +/- one
            // coarse step) so the full-res diamond converges to the exact optimum
            // within the basin the coarse pass identified.
            Span<(int X, int Y)> seeds = stackalloc (int, int)[]
            {
                (coarseX * f, coarseY * f),
                (coarseX * f - f, coarseY * f),
                (coarseX * f + f, coarseY * f),
                (coarseX * f, coarseY * f - f),
                (coarseX * f, coarseY * f + f),
            };

            foreach (var (sx, sy) in seeds)
            {
                RunDiamond(source, template, padX, padY,
                           Math.Clamp(sx, 0, padX), Math.Clamp(sy, 0, padY),
                           cache, ref errorBest, ref xBest, ref yBest);
            }
        }
        else
        {
            // Template too small to downsample meaningfully: fall back to seeding the
            // full-res diamond from the window corners + centre.
            Span<(int X, int Y)> seeds = stackalloc (int, int)[]
            {
                (0, 0),
                (padX / 2, padY / 2),
                (padX, 0),
                (0, padY),
                (padX, padY),
            };

            foreach (var (sx, sy) in seeds)
            {
                RunDiamond(source, template, padX, padY, sx, sy, cache, ref errorBest, ref xBest, ref yBest);
            }
        }

        return new EstimatedMotion {
            X = xBest,
            Y = yBest,
            Error = (float)errorBest / n
        };
    }

    /// <summary>
    /// Downsamples <paramref name="src"/> by <see cref="DownsampleFactor"/> using a
    /// box (averaging) filter, preserving the same (x first index, y second index)
    /// layout so the same motion-search convention applies. Output size is
    /// Height/factor x Width/factor (floor division).
    /// </summary>
    private static Span2D<byte> Downsample(Span2D<byte> src)
    {
        int f = DownsampleFactor;
        int h = src.Height / f;
        int w = src.Width / f;
        var coarse = new byte[h, w];

        for (int x = 0; x < h; x++)
        for (int y = 0; y < w; y++)
        {
            long sum = 0;
            for (int i = 0; i < f; i++)
            for (int j = 0; j < f; j++)
                sum += src[x * f + i, y * f + j];
            coarse[x, y] = (byte)(sum / (f * f));
        }

        return new Span2D<byte>(coarse);
    }

    /// <summary>
    /// Performs an LDSP+SDSP diamond search seeded at (seedX, seedY), updating the
    /// global best (via <paramref name="errorBest"/>/<paramref name="xBest"/>/<paramref name="yBest"/>)
    /// as better candidates are found. All candidates go through <see cref="Evaluate"/>,
    /// which reuses the shared <paramref name="cache"/>.
    /// </summary>
    private static void RunDiamond(
        Span2D<byte> source, Span2D<byte> template,
        int padX, int padY, int seedX, int seedY,
        Dictionary<(int X, int Y), long> cache,
        ref long globalBest, ref int globalX, ref int globalY)
    {
        int cx = seedX;
        int cy = seedY;

        // --- Large Diamond Search Pattern (step 2) ---
        // Iterate the 9-point diamond, tracking the best point via LOCAL bests
        // (the current seed's basin) so the diamond descends within its own basin.
        // The global best is updated whenever a better candidate is found.
        while (true)
        {
            bool centerIsBest = true;
            long centerBest = long.MaxValue;
            int bestX = cx;
            int bestY = cy;

            foreach (var (dx, dy) in LargeDiamond)
            {
                int px = cx + dx;
                int py = cy + dy;
                long sad = Evaluate(source, template, padX, padY, px, py, globalBest, cache);

                if (sad < centerBest)
                {
                    centerBest = sad;
                    bestX = px;
                    bestY = py;
                    centerIsBest = dx == 0 && dy == 0;
                }

                if (sad < globalBest)
                {
                    globalBest = sad;
                    globalX = px;
                    globalY = py;
                }
            }

            cx = bestX;
            cy = bestY;

            if (centerIsBest)
                break;
        }

        // --- Small Diamond Search Pattern (step 1) ---
        // Final single-step refinement around the converged centre.
        foreach (var (dx, dy) in SmallDiamond)
        {
            int px = cx + dx;
            int py = cy + dy;
            long sad = Evaluate(source, template, padX, padY, px, py, globalBest, cache);

            if (sad < globalBest)
            {
                globalBest = sad;
                globalX = px;
                globalY = py;
            }
        }
    }

    /// <summary>
    /// Evaluates the SAD of a candidate offset (px, py), clamped to the valid
    /// window. Out-of-bounds and already-computed candidates return without doing
    /// any pixel work. When the partial SAD already meets <paramref name="abortThreshold"/>,
    /// <see cref="long.MaxValue"/> is returned to signal "not better than the current best".
    /// </summary>
    private static long Evaluate(
        Span2D<byte> source, Span2D<byte> template,
        int padX, int padY, int px, int py,
        long abortThreshold, Dictionary<(int X, int Y), long> cache)
    {
        if (px < 0 || px > padX || py < 0 || py > padY)
            return long.MaxValue;

        if (cache.TryGetValue((px, py), out long cached))
            return cached;

        long sad = Sad(source, template, px, py, abortThreshold);
        cache[(px, py)] = sad;
        return sad;
    }

    /// <summary>
    /// Sum of absolute differences between the template and the source at offset
    /// (dx, dy). The inner loop is vectorized with System.Numerics.Vector when the
    /// hardware supports it, with a scalar tail for the remaining pixels.
    ///
    /// Vector&lt;byte&gt;.Count is 16 on ARM64 (NEON) and 32 on x64 (AVX2).
    /// The first array index is x; the second index (y) is contiguous in memory,
    /// so GetRowSpan(x + dx) is the vertical line at x + dx and we slide along y.
    /// </summary>
    private static long Sad(Span2D<byte> source, Span2D<byte> template, int dx, int dy, long abortThreshold)
    {
        long sad = 0;
        int vecSize = Vector<byte>.Count;

        for (int x = 0; x < template.Height; x++)
        {
            ReadOnlySpan<byte> srcCol = source.GetRowSpan(x + dx);
            ReadOnlySpan<byte> tmpCol = template.GetRowSpan(x);

            int y = 0;
            int height = template.Width;

            // Unified Hardware-Accelerated SIMD loop
            if (Vector.IsHardwareAccelerated && height >= vecSize)
            {
                // Accumulate into ushorts to prevent byte overflow (max 255)
                Vector<ushort> acc = Vector<ushort>.Zero;

                for (; y <= height - vecSize; y += vecSize)
                {
                    // Slice to the current offset, the JIT converts this directly into unaligned vector loads (vld1q_u8 / vmovdqu)
                    var vSrc = new Vector<byte>(srcCol.Slice(y + dy));
                    var vTmp = new Vector<byte>(tmpCol.Slice(y));

                    // Cross-platform Absolute Difference trick for unsigned bytes: Max(a, b) - Min(a, b)
                    var max = Vector.Max(vSrc, vTmp);
                    var min = Vector.Min(vSrc, vTmp);
                    var diff = Vector.Subtract(max, min);

                    // Widen the 8-bit differences into 16-bit to safely add them up
                    Vector.Widen(diff, out Vector<ushort> diffLow, out Vector<ushort> diffHigh);

                    acc = Vector.Add(acc, diffLow);
                    acc = Vector.Add(acc, diffHigh);
                }

                sad += Vector.Sum(acc);
            }

            // Scalar fallback handles remaining pixels (e.g., if height is not a multiple of the vector size)
            for (; y < height; y++)
            {
                sad += Math.Abs(tmpCol[y] - srcCol[y + dy]);
            }

            // Early abort: once the partial SAD is no better than the current best,
            // there is no point computing the remaining rows.
            if (sad >= abortThreshold)
                return long.MaxValue;
        }

        return sad;
    }
}