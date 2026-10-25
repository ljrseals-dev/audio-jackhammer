using System.Numerics;

namespace Denoiser.Models.DeepFilterNet;

public interface IDeepFilterKernel
{
    void Apply(ReadOnlySpan<Complex> noisyWindow, ReadOnlySpan<float> coefs, Span<Complex> maskedSpec);
}

public sealed class DeepFilterKernelImpl : IDeepFilterKernel
{
    public void Apply(ReadOnlySpan<Complex> w, ReadOnlySpan<float> c, Span<Complex> m)
        => DeepFilterKernel.Apply(w, c, m);
}
