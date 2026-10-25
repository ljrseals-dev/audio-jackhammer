using System.Numerics;

namespace Denoiser.Models.DeepFilterNet;

public interface IErbFilterbank
{
    void Forward(ReadOnlySpan<Complex> spec, Span<float> erb);
    void Inverse(ReadOnlySpan<float> erbGain, Span<float> binGain);
}
