using System.Numerics;

namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// 5-tap centered complex FIR applied to the low 96 bins:
///   Y[t,k] = Σₙ₌₀⁴ c[n,t,k] · X[t−2+n,k]
/// Tap 2 is the current frame; taps 0–1 past, 3–4 future (df_lookahead=2).
/// </summary>
public static class DeepFilterKernel
{
    public const int Order = 5;
    public const int Lookahead = 2;
    public const int DfBins = 96;

    public static void Apply(
        ReadOnlySpan<Complex> noisyWindow, // length Order × Bins
        ReadOnlySpan<float> coefs,         // length Order × DfBins × 2
        Span<Complex> maskedSpec)          // in: ERB-masked; out: DF on low bins
    {
        int bins = DeepFilterNetStft.Bins;
        if (noisyWindow.Length != Order * bins)
            throw new ArgumentException($"noisyWindow must be {Order} × {bins}.", nameof(noisyWindow));
        if (coefs.Length != Order * DfBins * 2)
            throw new ArgumentException($"coefs must be {Order} × {DfBins} × 2.", nameof(coefs));
        if (maskedSpec.Length != bins)
            throw new ArgumentException("maskedSpec must be 481 bins.", nameof(maskedSpec));

        for (int k = 0; k < DfBins; k++)
        {
            Complex acc = Complex.Zero;
            for (int n = 0; n < Order; n++)
            {
                Complex c = new(
                    coefs[(n * DfBins + k) * 2],
                    coefs[(n * DfBins + k) * 2 + 1]);
                Complex x = noisyWindow[n * bins + k];
                acc += c * x;
            }
            maskedSpec[k] = acc;
        }
        // Bins 96..480 untouched
    }
}
