using System.Numerics;
using UBCodec.Core.Encoder;
using UBCodec.Core.Utils;

namespace UBCodec.Tests.Encoder;

public class DistortionTest
{
    private static readonly Random _rng = new(1234);

    private static byte[] RandomBytes(int n)
    {
        var data = new byte[n];
        _rng.NextBytes(data);
        return data;
    }

    /// <summary>Reference scalar sum of squared differences.</summary>
    private static long NaiveSsd(byte[] a, byte[] b)
    {
        long sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int d = a[i] - b[i];
            sum += (long)d * d;
        }
        return sum;
    }

    private static int[] TestLengths()
    {
        int vs = Vector<byte>.Count;
        return
        [
            1,
            vs - 1,
            vs,
            vs + 1,
            3 * vs,
            1000,
            4096 + 7,   // not a multiple of vector size
        ];
    }

    [Test]
    public void SumSquaredDiff_MatchesScalar_AcrossSizes()
    {
        foreach (var n in TestLengths())
        {
            var a = RandomBytes(n);
            var b = RandomBytes(n);

            long expected = NaiveSsd(a, b);
            long actual = Distortion.SumSquaredDiff(a, b);

            Assert.That(actual, Is.EqualTo(expected), $"mismatch at length {n}");
        }
    }

    [Test]
    public void SumSquaredDiff_Identical_IsZero()
    {
        var a = RandomBytes(512);
        Assert.That(Distortion.SumSquaredDiff(a, a), Is.Zero);
    }

    [Test]
    public void SumSquaredDiff_ExactConstantShift_IsExact()
    {
        // a[i] = i, b[i] = i + 1 -> ssd = n
        var n = 300;
        var a = new byte[n];
        var b = new byte[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = (byte)(i % 200);
            b[i] = (byte)((i % 200) + 1);
        }
        Assert.That(Distortion.SumSquaredDiff(a, b), Is.EqualTo((long)n));
    }

    [Test]
    public void Mse_MatchesScalar()
    {
        int w = 33, h = 17;
        var a = new byte[w, h];
        var b = new byte[w, h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            a[x, y] = (byte)_rng.Next(256);
            b[x, y] = (byte)Math.Clamp(a[x, y] + _rng.Next(-20, 21), 0, 255);
        }

        double expected = NaiveSsd(Flatten(a), Flatten(b)) / (double)(w * h);
        double actual = Distortion.MSE(a, b);
        Assert.That(actual, Is.EqualTo(expected).Within(1e-9));
    }

    [Test]
    public void PSNR_Lossless_IsPositiveInfinity()
    {
        Assert.That(Distortion.PSNR(0), Is.EqualTo(double.PositiveInfinity));
    }

    [Test]
    public void PSNR_KnownValue()
    {
        // mse = 1  -> 10*log10(255^2) = ~48.1308
        Assert.That(Distortion.PSNR(1.0), Is.EqualTo(10 * Math.Log10(255.0 * 255.0)).Within(1e-9));
    }

    [Test]
    public void Measure_LosslessFrames_HasInfinitePsnr()
    {
        var frame = PlanarImage.FromSize(16, 16, 2);
        var result = Distortion.CollectMetrics(frame, frame, 64);
        Assert.That(result.Lossless, Is.True);
        Assert.That(result.LumaPsnr, Is.EqualTo(double.PositiveInfinity));
        Assert.That(result.WeightedPsnr, Is.EqualTo(double.PositiveInfinity));
        Assert.That(result.Bpp, Is.EqualTo(64.0 * 8 / (16 * 16)).Within(1e-9));
    }

    [Test]
    public void Measure_WeightedPsnr_IsWeightedAverage()
    {
        var a = PlanarImage.FromSize(8, 8, 2);
        var b = PlanarImage.FromSize(8, 8, 2);
        // Corrupt a few pixels in every plane with finite offsets.
        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                b.LBuffer[x, y] = 100;
                b.C1Buffer[x, y] = 200;
            }
        }

        var result = Distortion.CollectMetrics(a, b, 100);

        double lumaPsnr = Distortion.PSNR(Distortion.MSE(a.LBuffer, b.LBuffer));
        double c1Psnr = Distortion.PSNR(Distortion.MSE(a.C1Buffer, b.C1Buffer));
        double c2Psnr = Distortion.PSNR(Distortion.MSE(a.C2Buffer, b.C2Buffer));

        Assert.That(result.LumaPsnr, Is.EqualTo(lumaPsnr).Within(1e-9));
        Assert.That(result.WeightedPsnr,
            Is.EqualTo((6 * lumaPsnr + c1Psnr + c2Psnr) / 8).Within(1e-9));
    }

    [Test]
    public void CollectMetrics_Bpp_IsCorrect()
    {
        var a = PlanarImage.FromSize(1920, 1080, 2);
        var b = PlanarImage.FromSize(1920, 1080, 2);
        var result = Distortion.CollectMetrics(a, b, 1_000_000);
        Assert.That(result.Bpp, Is.EqualTo(8_000_000.0 / (1920 * 1080)).Within(1e-9));
    }

    private static byte[] Flatten(byte[,] data)
    {
        int w = data.GetLength(0), h = data.GetLength(1);
        var flat = new byte[w * h];
        int k = 0;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            flat[k++] = data[x, y];
        return flat;
    }
}
