using System.Numerics;

namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// FftSharp Bluestein backend — O(N log N) for arbitrary N including 960.
/// Bluestein.Inverse is unnormalized; we divide by N on inverse.
/// </summary>
public sealed class FftSharpBackend : IFft
{
    private readonly int _n;
    public int Size => _n;

    public FftSharpBackend(int n) => _n = n;

    public void Forward(ReadOnlySpan<double> realInput, Span<Complex> spectrum)
    {
        if (realInput.Length != _n)
            throw new ArgumentException("Shape mismatch.", nameof(realInput));

        Complex[] full = FftSharp.Bluestein.Forward(realInput.ToArray());
        int bins = _n / 2 + 1;
        for (int k = 0; k < bins; k++)
            spectrum[k] = full[k];
    }

    public void Inverse(ReadOnlySpan<Complex> spectrum, Span<double> realOutput)
    {
        int bins = _n / 2 + 1;
        if (spectrum.Length < bins || realOutput.Length != _n)
            throw new ArgumentException("Shape mismatch.");

        var full = new Complex[_n];
        for (int k = 0; k < bins; k++)
            full[k] = spectrum[k];
        for (int k = 1; k < bins - 1; k++)
            full[_n - k] = Complex.Conjugate(spectrum[k]);
        if ((_n & 1) == 0)
            full[_n / 2] = new Complex(spectrum[bins - 1].Real, 0);

        FftSharp.Bluestein.Inverse(full);
        for (int i = 0; i < _n; i++)
            realOutput[i] = full[i].Real / _n;
    }
}
