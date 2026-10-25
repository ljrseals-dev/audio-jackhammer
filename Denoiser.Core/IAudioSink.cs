namespace Denoiser.Core;

/// <summary>
/// Accepts mono 48 kHz float32 PCM and writes it to a file or playback device.
/// </summary>
public interface IAudioSink : IDisposable
{
    /// <summary>Writes samples. May buffer internally.</summary>
    void Write(ReadOnlySpan<float> samples);

    /// <summary>Flushes any remaining buffered data and finalizes the output.</summary>
    void Flush();
}
