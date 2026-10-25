namespace Denoiser.Realtime;

/// <summary>Capture callback: samples are valid only for the duration of the call.</summary>
public delegate void CaptureDataHandler(ReadOnlySpan<float> samples);

/// <summary>Render callback: fill the span; write silence on underrun.</summary>
public delegate void RenderRequestHandler(Span<float> buffer);

public interface IAudioCaptureDevice : IDisposable
{
    int SampleRate { get; }
    int Channels { get; }
    event CaptureDataHandler? DataAvailable;
    void Start();
    void Stop();
}

public interface IAudioRenderDevice : IDisposable
{
    int SampleRate { get; }
    int Channels { get; }
    event RenderRequestHandler? RenderRequested;
    void Start();
    void Stop();
}
