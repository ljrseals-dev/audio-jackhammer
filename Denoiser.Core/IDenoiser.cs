namespace Denoiser.Core;

/// <summary>
/// Core denoising engine. Processes mono float32 PCM frames in-place or returns a new buffer.
/// Frame size is model-specific (e.g. 480 samples for RNNoise).
/// </summary>
public interface IDenoiser : IDisposable
{
    /// <summary>Preferred frame size in samples. Callers should feed multiples of this.</summary>
    int FrameSize { get; }

    /// <summary>Sample rate expected by the model (typically 48000).</summary>
    int SampleRate { get; }

    /// <summary>Human-readable model name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// Processes a frame of mono float32 samples. Length should be a multiple of <see cref="FrameSize"/>.
    /// May process in-place; the returned span may alias the input.
    /// </summary>
    void Process(Span<float> frame);
}
