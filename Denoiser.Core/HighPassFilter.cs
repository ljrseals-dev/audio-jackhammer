namespace Denoiser.Core;

/// <summary>
/// 2nd-order Butterworth high-pass filter.
/// Default cutoff 90 Hz @ 48 kHz sample rate — strips machinery rumble
/// that models often misclassify as speech energy.
/// </summary>
public sealed class HighPassFilter
{
    private readonly float _a0, _a1, _a2, _b1, _b2;
    private float _x1, _x2, _y1, _y2;

    public float CutoffHz { get; }

    public HighPassFilter(float cutoffHz = 90f, int sampleRate = 48000)
    {
        if (cutoffHz <= 0 || cutoffHz >= sampleRate / 2f)
            throw new ArgumentOutOfRangeException(nameof(cutoffHz));

        CutoffHz = cutoffHz;

        // Bilinear transform of analog 2nd-order Butterworth HPF
        double w0 = 2.0 * Math.PI * cutoffHz / sampleRate;
        double cosw0 = Math.Cos(w0);
        double sinw0 = Math.Sin(w0);
        double alpha = sinw0 / (2.0 * Math.Sqrt(2.0) / 2.0); // Q = 1/√2 for Butterworth

        double b0 = (1.0 + cosw0) / 2.0;
        double b1 = -(1.0 + cosw0);
        double b2 = (1.0 + cosw0) / 2.0;
        double a0 = 1.0 + alpha;
        double a1 = -2.0 * cosw0;
        double a2 = 1.0 - alpha;

        _a0 = (float)(b0 / a0);
        _a1 = (float)(b1 / a0);
        _a2 = (float)(b2 / a0);
        _b1 = (float)(a1 / a0);
        _b2 = (float)(a2 / a0);
    }

    /// <summary>Process samples in-place.</summary>
    public void Process(Span<float> samples)
    {
        for (int i = 0; i < samples.Length; i++)
        {
            float x0 = samples[i];
            float y0 = _a0 * x0 + _a1 * _x1 + _a2 * _x2 - _b1 * _y1 - _b2 * _y2;
            _x2 = _x1;
            _x1 = x0;
            _y2 = _y1;
            _y1 = y0;
            samples[i] = y0;
        }
    }

    public void Reset()
    {
        _x1 = _x2 = _y1 = _y2 = 0f;
    }
}
