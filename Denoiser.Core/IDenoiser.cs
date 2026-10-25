namespace Denoiser.Core;

/// <summary>
/// Core denoising engine. Processes mono float32 PCM frames in-place.
/// Frame size is model-specific (e.g. 480 samples for RNNoise).
/// </summary>
public interface IDenoiser : IDisposable
{
    /// <summary>Preferred frame size in samples. Callers must feed exact multiples via FrameAccumulator.</summary>
    int FrameSize { get; }

    /// <summary>Sample rate expected by the model (typically 48000).</summary>
    int SampleRate { get; }

    /// <summary>Human-readable model name for logging.</summary>
    string Name { get; }

    /// <summary>
    /// When true, the pipeline should wrap this instance in an <see cref="OverlapAddAdapter"/>.
    /// Default is false (RNNoise, NullDenoiser).
    /// </summary>
    bool RequiresOverlapAdd => false;

    /// <summary>
    /// Processes a frame of mono float32 samples of length exactly <see cref="FrameSize"/>.
    /// Processes in-place.
    /// </summary>
    void Process(Span<float> frame);
}
