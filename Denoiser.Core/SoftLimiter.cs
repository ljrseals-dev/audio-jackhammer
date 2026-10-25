namespace Denoiser.Core;

/// <summary>
/// Peak limiter with optional look-ahead and instant attack.
///
/// Without look-ahead (default): soft-knee compression above (ceiling - knee).
/// With look-ahead: delays by lookAheadMs, instant attack on peaks, smooth release.
/// Constraint: releaseMs >= 2 * lookAheadMs (enforced at construction).
/// </summary>
public sealed class SoftLimiter
{
    private readonly float _ceiling;
    private readonly float _knee;
    private readonly bool _lookAhead;

    private readonly float[] _delayLine;
    private readonly int _delayLength;
    private readonly int _delayMask;
    private readonly float _releaseCoeff;

    private int _pos;
    private float _gain = 1f;

    public SoftLimiter(
        float threshold = 0.95f,
        float knee = 0.05f,
        bool lookAhead = false,
        int sampleRate = 48000,
        float lookAheadMs = 5f,
        float releaseMs = 50f)
    {
        if (threshold <= 0f || threshold > 1f)
            throw new ArgumentOutOfRangeException(nameof(threshold));
        if (knee < 0f || knee > threshold)
            throw new ArgumentOutOfRangeException(nameof(knee));
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (lookAhead && lookAheadMs <= 0f)
            throw new ArgumentOutOfRangeException(nameof(lookAheadMs));
        if (lookAhead && releaseMs < 2f * lookAheadMs)
            throw new ArgumentOutOfRangeException(nameof(releaseMs),
                "releaseMs must be at least 2× lookAheadMs to avoid gain pumping.");

        _ceiling = threshold;
        _knee = knee;
        _lookAhead = lookAhead;

        if (lookAhead)
        {
            int raw = Math.Max(1, (int)MathF.Round(sampleRate * lookAheadMs / 1000f));
            int pow2 = 1;
            while (pow2 < raw) pow2 <<= 1;
            _delayLength = pow2;
            _delayMask = pow2 - 1;
            _delayLine = new float[pow2];
            _releaseCoeff = 1f / Math.Max(1f, (int)(sampleRate * releaseMs / 1000f));
        }
        else
        {
            _delayLength = 0;
            _delayMask = 0;
            _delayLine = Array.Empty<float>();
            _releaseCoeff = 0f;
        }
    }

    public bool LookAheadEnabled => _lookAhead;
    public int DelaySamples => _delayLength;

    public void Process(Span<float> buffer)
    {
        if (buffer.Length == 0) return;

        if (!_lookAhead)
        {
            ApplySoftKnee(buffer);
            return;
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float x = buffer[i];
            float peak = MathF.Abs(x);
            float targetGain = peak <= _ceiling ? 1f : _ceiling / peak;

            if (targetGain < _gain)
                _gain = targetGain;
            else
                _gain += (targetGain - _gain) * _releaseCoeff;

            float delayed = _delayLine[_pos];
            _delayLine[_pos] = x;
            _pos = (_pos + 1) & _delayMask;

            buffer[i] = delayed * _gain;
        }

        ApplySoftKnee(buffer);
    }

    private void ApplySoftKnee(Span<float> buffer)
    {
        float threshold = _ceiling - _knee;
        for (int i = 0; i < buffer.Length; i++)
        {
            float x = buffer[i];
            float a = MathF.Abs(x);
            if (a <= threshold) continue;
            float over = a - threshold;
            float compressed = threshold + _knee * MathF.Tanh(over / _knee);
            buffer[i] = MathF.CopySign(compressed, x);
        }
    }
}
