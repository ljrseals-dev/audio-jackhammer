namespace Denoiser.Core;

/// <summary>
/// Streams normalized mono 48 kHz float32 PCM samples.
/// Implementations must not load the entire file into memory.
/// </summary>
public interface IAudioSource : IDisposable
{
    /// <summary>Sample rate of the streamed PCM (always 48000 at the pipeline boundary).</summary>
    int SampleRate { get; }

    /// <summary>Number of channels (always 1 = mono at the pipeline boundary).</summary>
    int Channels { get; }

    /// <summary>
    /// Reads the next chunk of mono float32 samples into <paramref name="buffer"/>.
    /// Returns the number of samples written (0 = end of stream).
    /// </summary>
    int Read(Span<float> buffer);

    /// <summary>Total duration if known; null for live sources.</summary>
    TimeSpan? Duration { get; }
}
