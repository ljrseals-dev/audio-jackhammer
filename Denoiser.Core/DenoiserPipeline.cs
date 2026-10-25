using Microsoft.Extensions.Logging;

namespace Denoiser.Core;

/// <summary>
/// Orchestrates Source → HighPass → Denoiser → SoftLimiter → Sink.
/// Processes in model-frame multiples with optional SNR logging.
/// </summary>
public sealed class DenoiserPipeline : IDisposable
{
    private readonly IAudioSource _source;
    private readonly IDenoiser _denoiser;
    private readonly IAudioSink _sink;
    private readonly HighPassFilter? _highPass;
    private readonly SoftLimiter _limiter;
    private readonly ILogger? _logger;
    private readonly int _frameSize;
    private readonly float[] _buffer;
    private bool _disposed;

    public DenoiserPipeline(
        IAudioSource source,
        IDenoiser denoiser,
        IAudioSink sink,
        float highPassHz = 90f,
        bool enableHighPass = true,
        ILogger? logger = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _denoiser = denoiser ?? throw new ArgumentNullException(nameof(denoiser));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _logger = logger;
        _frameSize = denoiser.FrameSize;
        _buffer = new float[_frameSize * 4]; // process a few frames at a time
        _limiter = new SoftLimiter();
        _highPass = enableHighPass ? new HighPassFilter(highPassHz, source.SampleRate) : null;
    }

    /// <summary>
    /// Runs the full pipeline until the source is exhausted.
    /// Returns total samples processed and wall-clock elapsed time.
    /// </summary>
    public (long SamplesProcessed, TimeSpan Elapsed) Run()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long totalSamples = 0;
        int blockIndex = 0;

        while (true)
        {
            int read = _source.Read(_buffer);
            if (read == 0)
                break;

            var frame = _buffer.AsSpan(0, read);

            _highPass?.Process(frame);
            _denoiser.Process(frame);
            _limiter.Process(frame);

            if (_logger is not null && blockIndex % 50 == 0)
            {
                float snr = SnrEstimator.EstimateDb(frame);
                _logger.LogDebug("Block {Block}: SNR≈{Snr:F1} dB, samples={Samples}", blockIndex, snr, read);
            }

            _sink.Write(frame);
            totalSamples += read;
            blockIndex++;
        }

        _sink.Flush();
        sw.Stop();
        return (totalSamples, sw.Elapsed);
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
