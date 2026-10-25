namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// 32-band ERB filterbank. Loads canonical matrices from auxiliary bin when present;
/// otherwise builds a simple contiguous partition (test-only, not production quality).
/// </summary>
public sealed class ErbFilterbank : IErbFilterbank
{
    public const int Bands = 32;

    private readonly float[] _forward; // [bins, bands] row-major
    private readonly float[] _inverse; // [bands, bins] row-major

    public ErbFilterbank(string? auxiliaryBinPath = null)
    {
        int bins = DeepFilterNetStft.Bins;
        _forward = new float[bins * Bands];
        _inverse = new float[Bands * bins];

        if (!string.IsNullOrEmpty(auxiliaryBinPath) && File.Exists(auxiliaryBinPath))
        {
            var bytes = File.ReadAllBytes(auxiliaryBinPath);
            int expected = (bins * Bands + Bands * bins + DeepFilterNetStft.FftSize) * 4;
            if (bytes.Length < bins * Bands * 4 + Bands * bins * 4)
                throw new InvalidDataException(
                    $"Auxiliary bin has {bytes.Length} bytes; need at least {bins * Bands * 4 + Bands * bins * 4}.");
            Buffer.BlockCopy(bytes, 0, _forward, 0, _forward.Length * 4);
            Buffer.BlockCopy(bytes, _forward.Length * 4, _inverse, 0, _inverse.Length * 4);
        }
        else
        {
            // Fallback: equal-width partitions for unit tests without aux file.
            int binsPerBand = bins / Bands;
            for (int b = 0; b < Bands; b++)
            {
                int start = b * binsPerBand;
                int end = (b == Bands - 1) ? bins : start + binsPerBand;
                float w = 1f / Math.Max(1, end - start);
                for (int k = start; k < end; k++)
                {
                    _forward[k * Bands + b] = w;
                    _inverse[b * bins + k] = 1f; // gain passthrough per band
                }
            }
        }
    }

    public void Forward(ReadOnlySpan<System.Numerics.Complex> spec, Span<float> erb)
    {
        if (erb.Length != Bands) throw new ArgumentException(nameof(erb));
        int bins = DeepFilterNetStft.Bins;
        for (int b = 0; b < Bands; b++)
        {
            double sum = 0;
            for (int k = 0; k < bins; k++)
                sum += _forward[k * Bands + b] * spec[k].Magnitude;
            erb[b] = (float)sum;
        }
    }

    public void Inverse(ReadOnlySpan<float> erbGain, Span<float> binGain)
    {
        if (erbGain.Length != Bands) throw new ArgumentException(nameof(erbGain));
        if (binGain.Length != DeepFilterNetStft.Bins) throw new ArgumentException(nameof(binGain));
        int bins = DeepFilterNetStft.Bins;
        for (int k = 0; k < bins; k++)
        {
            double sum = 0;
            for (int b = 0; b < Bands; b++)
                sum += _inverse[b * bins + k] * erbGain[b];
            binGain[k] = (float)sum;
        }
    }
}
