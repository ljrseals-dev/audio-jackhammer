namespace Denoiser.Core;

/// <summary>
/// Rough RMS-based SNR estimate per block.
/// Construction noise swings wildly; this provides visibility for logging.
/// Supports both static one-shot estimates and an instance that tracks LastDb.
/// </summary>
public sealed class SnrEstimator
{
    public float LastDb { get; private set; }

    /// <summary>Observe a block and update <see cref="LastDb"/>.</summary>
    public void Observe(ReadOnlySpan<float> samples)
    {
        LastDb = EstimateDb(samples);
    }

    /// <summary>
    /// Estimates SNR in dB assuming the "noise floor" is the quieter portion
    /// of the block (bottom 20% of frame RMS windows) versus overall RMS.
    /// Returns a rough value suitable for logging, not scientific measurement.
    /// </summary>
    public static float EstimateDb(ReadOnlySpan<float> samples, int windowSize = 160)
    {
        if (samples.Length == 0)
            return 0f;

        int windows = Math.Max(1, samples.Length / windowSize);
        Span<float> rms = stackalloc float[windows];
        float totalEnergy = 0f;

        for (int w = 0; w < windows; w++)
        {
            int start = w * windowSize;
            int len = Math.Min(windowSize, samples.Length - start);
            float e = 0f;
            for (int i = 0; i < len; i++)
            {
                float s = samples[start + i];
                e += s * s;
            }
            rms[w] = (float)Math.Sqrt(e / len);
            totalEnergy += e;
        }

        float overallRms = (float)Math.Sqrt(totalEnergy / samples.Length);

        float[] sorted = rms.ToArray();
        Array.Sort(sorted);
        float noiseFloor = sorted[Math.Max(0, sorted.Length / 5)];

        if (noiseFloor < 1e-10f)
            noiseFloor = 1e-10f;
        if (overallRms < 1e-10f)
            return 0f;

        return 20f * (float)Math.Log10(overallRms / noiseFloor);
    }
}
