namespace Denoiser.Core;

/// <summary>
/// Pass-through denoiser for A/B testing and regression tests.
/// Preserves samples bit-exact.
/// </summary>
public sealed class NullDenoiser : IDenoiser
{
    public int FrameSize => 480;
    public int SampleRate => 48000;
    public string Name => "null";

    public void Process(Span<float> frame)
    {
        // Intentionally empty — pass-through
    }

    public void Dispose() { }
}
