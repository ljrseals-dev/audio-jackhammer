namespace Denoiser.Core;

/// <summary>
/// Soft peak limiter / normalizer to prevent clipping on sudden high-energy impacts
/// (jackhammers, rock drills, etc.) after denoising.
/// </summary>
public sealed class SoftLimiter
{
    private readonly float _threshold;
    private readonly float _knee;

    /// <param name="threshold">Level above which limiting begins (linear, e.g. 0.95).</param>
    /// <param name="knee">Soft-knee width in linear amplitude.</param>
    public SoftLimiter(float threshold = 0.95f, float knee = 0.05f)
    {
        _threshold = threshold;
        _knee = Math.Max(knee, 1e-6f);
    }

    /// <summary>Process samples in-place.</summary>
    public void Process(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            float x = samples[i];
            float ax = Math.Abs(x);
            if (ax <= _threshold - _knee)
                continue;

            float sign = Math.Sign(x);
            if (ax >= _threshold + _knee)
            {
                samples[i] = sign * _threshold;
            }
            else
            {
                // Soft knee: quadratic blend
                float t = (ax - (_threshold - _knee)) / (2f * _knee);
                float limited = _threshold - _knee + 2f * _knee * t * (1f - 0.5f * t);
                samples[i] = sign * limited;
            }
        }
    }
}
