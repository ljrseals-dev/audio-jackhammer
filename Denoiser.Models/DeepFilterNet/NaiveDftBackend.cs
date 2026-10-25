using System.Numerics;

namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// O(N²) DFT — reference backend for cross-validation (--fft naive).
/// </summary>
public sealed class NaiveDftBackend : IFft
{
    private readonly int _n;
    public int Size => _n;

    public NaiveDftBackend(int n) => _n = n;

    public void Forward(ReadOnlySpan<double> realInput, Span<Complex> spectrum)
    {
        int bins = _n / 2 + 1;
        if (realInput.Length != _n || spectrum.Length < bins)
            throw new ArgumentException("Shape mismatch.");

        for (int k = 0; k < bins; k++)
        {
            double sr = 0, si = 0;
            for (int t = 0; t < _n; t++)
            {
                double ang = -2.0 * Math.PI * k * t / _n;
                sr += realInput[t] * Math.Cos(ang);
                si += realInput[t] * Math.Sin(ang);
            }
            spectrum[k] = new Complex(sr, si);
        }
    }

    public void Inverse(ReadOnlySpan<Complex> spectrum, Span<double> realOutput)
    {
        int bins = _n / 2 + 1;
        if (spectrum.Length < bins || realOutput.Length != _n)
            throw new ArgumentException("Shape mismatch.");

        for (int t = 0; t < _n; t++)
        {
            double sr = spectrum[0].Real;
            for (int k = 1; k < bins - 1; k++)
            {
                double ang = 2.0 * Math.PI * k * t / _n;
                // Hermitian: include conjugate pair
                sr += 2 * (spectrum[k].Real * Math.Cos(ang) - spectrum[k].Imaginary * Math.Sin(ang));
            }
            // Nyquist
            if ((_n & 1) == 0)
            {
                double ang = Math.PI * t;
                sr += spectrum[bins - 1].Real * Math.Cos(ang);
            }
            realOutput[t] = sr / _n;
        }
    }
}
