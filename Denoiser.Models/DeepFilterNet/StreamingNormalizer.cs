namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// Running mean with alpha=0.99.
/// ERB mean initialized linearly from −60 dB to −90 dB;
/// complex unit state from 0.001 to 0.0001.
/// </summary>
public sealed class StreamingNormalizer
{
    public const float Alpha = 0.99f;

    private readonly float[] _erbMean = new float[32];
    private readonly float[] _unitMeanRe = new float[96];
    private readonly float[] _unitMeanIm = new float[96];

    public StreamingNormalizer()
    {
        for (int b = 0; b < 32; b++)
            _erbMean[b] = -60f + (-90f - -60f) * (b / 31f);
        for (int k = 0; k < 96; k++)
        {
            _unitMeanRe[k] = 0.001f + (0.0001f - 0.001f) * (k / 95f);
            _unitMeanIm[k] = 0f;
        }
    }

    public void NormalizeErb(Span<float> erb)
    {
        for (int b = 0; b < erb.Length; b++)
        {
            _erbMean[b] = Alpha * _erbMean[b] + (1 - Alpha) * erb[b];
            erb[b] -= _erbMean[b];
        }
    }

    public void NormalizeSpec(ReadOnlySpan<System.Numerics.Complex> spec, Span<float> featSpec)
    {
        // featSpec layout: [2, 96] = real then imaginary
        for (int k = 0; k < 96; k++)
        {
            float re = (float)spec[k].Real;
            float im = (float)spec[k].Imaginary;
            _unitMeanRe[k] = Alpha * _unitMeanRe[k] + (1 - Alpha) * re;
            _unitMeanIm[k] = Alpha * _unitMeanIm[k] + (1 - Alpha) * im;
            featSpec[k] = re - _unitMeanRe[k];
            featSpec[96 + k] = im - _unitMeanIm[k];
        }
    }
}
