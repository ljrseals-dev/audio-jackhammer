using Denoiser.Core;

namespace Denoiser.Tests.Support;

/// <summary>
/// Stateful identity-adjacent model for the handoff golden.
/// y[n] = 0.9*x[n] - 0.1*x[n-1] with cross-frame state.
/// </summary>
public sealed class DeterministicStubModel : IDenoiser
{
    private float _prev;

    public int FrameSize => 480;
    public int SampleRate => 48000;
    public string Name => "stub";
    public bool RequiresOverlapAdd => false;

    public void Process(Span<float> frame)
    {
        if (frame.Length != FrameSize)
            throw new ArgumentException($"Expected {FrameSize} samples, got {frame.Length}.", nameof(frame));

        for (int i = 0; i < frame.Length; i++)
        {
            float x = frame[i];
            frame[i] = 0.9f * x - 0.1f * _prev;
            _prev = x;
        }
    }

    public void Dispose() { }
}
