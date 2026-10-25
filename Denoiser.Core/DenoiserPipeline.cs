using Microsoft.Extensions.Logging;

namespace Denoiser.Core;

/// <summary>
/// Orchestrates Source → HighPass → Denoiser → SoftLimiter → Sink.
/// All model calls use exact FrameSize via FrameAccumulator.
/// Final zero-padded residual is emitted at end-of-stream; if the denoiser
/// implements IFlushableDenoiser its pending tail is also drained.
/// </summary>
public sealed class DenoiserPipeline : IDisposable
{
    private readonly IAudioSource _source;
    private readonly IDenoiser _denoiser;
    private readonly IAudioSink _sink;
    private readonly HighPassFilter? _highPass;
    private readonly SoftLimiter _limiter;
    private readonly FrameAccumulator _framer;
    private readonly SnrEstimator _snr;
    private readonly ILogger? _logger;
    private readonly float[] _readBuffer;
    private readonly float[] _flushBuf;
    private long _totalSamples;
    private int _blockIndex;
    private bool _disposed;

    public DenoiserPipeline(
        IAudioSource source,
        IDenoiser denoiser,
        IAudioSink sink,
        float highPassHz = 90f,
        bool enableHighPass = true,
        SoftLimiter? limiter = null,
        SnrEstimator? snr = null,
        ILogger? logger = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _denoiser = denoiser ?? throw new ArgumentNullException(nameof(denoiser));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _logger = logger;
        _framer = new FrameAccumulator(denoiser.FrameSize);
        _readBuffer = new float[Math.Max(denoiser.FrameSize * 4, 4096)];
        _flushBuf = new float[denoiser.FrameSize];
        _limiter = limiter ?? new SoftLimiter();
        _snr = snr ?? new SnrEstimator();
        _highPass = enableHighPass ? new HighPassFilter(highPassHz, source.SampleRate) : null;
    }

    public SnrEstimator Snr => _snr;

    /// <summary>
    /// Runs until the source is exhausted, flushes residual frame + OLA tail, then sink.
    /// Returns samples written (includes padding) and wall-clock elapsed.
    /// </summary>
    public (long SamplesProcessed, TimeSpan Elapsed) Run()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _totalSamples = 0;
        _blockIndex = 0;

        while (true)
        {
            int read = _source.Read(_readBuffer);
            if (read == 0)
                break;
            ProcessChunk(_readBuffer.AsSpan(0, read));
        }

        Complete();
        sw.Stop();
        return (_totalSamples, sw.Elapsed);
    }

    private void ProcessChunk(ReadOnlySpan<float> chunk)
    {
        _framer.Push(chunk, (frame, length) =>
        {
            ProcessOneFrame(frame.AsSpan(0, length));
        });
    }

    private void Complete()
    {
        // Drain residual partial frame (zero-padded to FrameSize).
        _framer.Flush((frame, length) =>
        {
            ProcessOneFrame(frame.AsSpan(0, length));
        });

        // Drain pending OLA tail if present.
        if (_denoiser is IFlushableDenoiser flushable)
        {
            int n = flushable.Flush(_flushBuf);
            if (n > 0)
            {
                var tail = _flushBuf.AsSpan(0, n);
                _limiter.Process(tail);
                _snr.Observe(tail);
                _sink.Write(tail);
                _totalSamples += n;
            }
        }

        _sink.Flush();
    }

    private void ProcessOneFrame(Span<float> frame)
    {
        _highPass?.Process(frame);
        _denoiser.Process(frame);
        _limiter.Process(frame);
        _snr.Observe(frame);

        if (_logger is not null && _blockIndex % 50 == 0)
        {
            _logger.LogDebug(
                "Block {Block}: SNR≈{Snr:F1} dB, samples={Samples}",
                _blockIndex, _snr.LastDb, frame.Length);
        }

        _sink.Write(frame);
        _totalSamples += frame.Length;
        _blockIndex++;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.Dispose();
        _denoiser.Dispose();
        _sink.Dispose();
    }
}
