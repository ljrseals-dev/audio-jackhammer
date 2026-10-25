using System.Numerics;

namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// 960-point real FFT, 480-sample hop, Vorbis analysis/synthesis window.
/// Synthesize folds the previous frame's tail automatically (no separate AddPendingTail).
/// </summary>
public sealed class DeepFilterNetStft
{
    public const int FftSize = 960;
    public const int HopSize = 480;
    public const int Bins = FftSize / 2 + 1; // 481

    private readonly float[] _window;
    private readonly float[] _history = new float[HopSize];
    private readonly float[] _frame = new float[FftSize];
    private readonly Complex[] _spectrum = new Complex[Bins];
    private readonly double[] _timeBuf = new double[FftSize];
    private readonly float[] _synthesisTail = new float[HopSize];
    private readonly IFft _fft;

    public DeepFilterNetStft(IFft? fft = null)
    {
        _window = VorbisWindow.Create(FftSize);
        _fft = fft ?? new FftSharpBackend(FftSize);
    }

    public ReadOnlySpan<Complex> Analyze(ReadOnlySpan<float> hop)
    {
        if (hop.Length != HopSize)
            throw new ArgumentException($"Expected {HopSize} samples.", nameof(hop));

        Array.Copy(_history, 0, _frame, 0, HopSize);
        hop.CopyTo(_frame.AsSpan(HopSize));
        hop.CopyTo(_history);

        for (int i = 0; i < FftSize; i++)
            _timeBuf[i] = _frame[i] * _window[i];

        _fft.Forward(_timeBuf, _spectrum);
        return _spectrum;
    }

    /// <summary>
    /// Obsolete: tail is now folded inside Synthesize. Kept as no-op for any stragglers.
    /// </summary>
    [Obsolete("Tail is folded automatically inside Synthesize. Do not call.")]
    public void AddPendingTail(Span<float> hop) { }

    /// <summary>
    /// IFFT + Vorbis synthesis window + overlap-add with previous frame's tail.
    /// </summary>
    public void Synthesize(ReadOnlySpan<Complex> spectrum, Span<float> outputHop)
    {
        if (spectrum.Length != Bins)
            throw new ArgumentException($"Expected {Bins} bins.", nameof(spectrum));
        if (outputHop.Length != HopSize)
            throw new ArgumentException($"Expected {HopSize} output samples.", nameof(outputHop));

        _fft.Inverse(spectrum, _timeBuf);

        // Accumulate: current first-half + previous second-half tail
        for (int i = 0; i < HopSize; i++)
        {
            float sample = (float)(_timeBuf[i] * _window[i]);
            outputHop[i] = sample + _synthesisTail[i];
        }

        // Save this frame's second half for the next call
        for (int i = HopSize; i < FftSize; i++)
            _synthesisTail[i - HopSize] = (float)(_timeBuf[i] * _window[i]);
    }
}
