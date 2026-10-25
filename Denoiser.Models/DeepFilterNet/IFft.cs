using System.Numerics;

namespace Denoiser.Models.DeepFilterNet;

public interface IFft
{
    int Size { get; }
    void Forward(ReadOnlySpan<double> realInput, Span<Complex> spectrum);
    void Inverse(ReadOnlySpan<Complex> spectrum, Span<double> realOutput);
}
