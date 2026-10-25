namespace Denoiser.Tests.Support;

public static class Dsp
{
    /// <summary>
    /// Band-limited RMS via windowed FFT. Units are arbitrary; use only for
    /// ratio comparisons between two measurements taken the same way.
    /// </summary>
    public static double BandRms(
        ReadOnlySpan<float> samples, int sampleRate, double loHz, double hiHz)
    {
        int n = 1;
        while (n < samples.Length) n <<= 1;

        var real = new double[n];
        var imag = new double[n];
        for (int i = 0; i < samples.Length; i++)
        {
            double w = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / Math.Max(1, samples.Length - 1)));
            real[i] = samples[i] * w;
        }

        Fft(real, imag);

        int binLo = Math.Max(1, (int)(loHz * n / sampleRate));
        int binHi = Math.Min(n / 2, (int)(hiHz * n / sampleRate));
        double power = 0;
        for (int k = binLo; k <= binHi; k++)
            power += real[k] * real[k] + imag[k] * imag[k];

        return Math.Sqrt(power / Math.Max(1, binHi - binLo + 1));
    }

    public static double TotalRms(ReadOnlySpan<float> samples)
    {
        double sum = 0;
        for (int i = 0; i < samples.Length; i++) sum += samples[i] * (double)samples[i];
        return Math.Sqrt(sum / Math.Max(1, samples.Length));
    }

    public static (double maxAbsDiff, int firstDiffIdx) CompareSamples(
        ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length) return (double.PositiveInfinity, -1);
        double maxDiff = 0;
        int firstIdx = -1;
        for (int i = 0; i < a.Length; i++)
        {
            double d = Math.Abs(a[i] - (double)b[i]);
            if (d > maxDiff) maxDiff = d;
            if (d > 0 && firstIdx < 0) firstIdx = i;
        }
        return (maxDiff, firstIdx);
    }

    private static void Fft(double[] real, double[] imag)
    {
        int n = real.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double ang = -2 * Math.PI / len;
            double wr = Math.Cos(ang), wi = Math.Sin(ang);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int j = 0; j < len / 2; j++)
                {
                    int u = i + j, v = i + j + len / 2;
                    double vr = real[v] * cr - imag[v] * ci;
                    double vi = real[v] * ci + imag[v] * cr;
                    real[v] = real[u] - vr; imag[v] = imag[u] - vi;
                    real[u] += vr;          imag[u] += vi;
                    double nr = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = nr;
                }
            }
        }
    }
}
