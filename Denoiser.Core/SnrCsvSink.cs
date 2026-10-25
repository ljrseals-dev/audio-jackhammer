namespace Denoiser.Core;

/// <summary>
/// Decorator that writes a CSV sidecar of SNR estimates alongside the audio.
/// Row cadence: one row every reportIntervalMs of audio time (not wall time).
/// </summary>
public sealed class SnrCsvSink : IAudioSink
{
    private readonly IAudioSink _inner;
    private readonly SnrEstimator _snr;
    private readonly int _sampleRate;
    private readonly long _reportIntervalSamples;
    private readonly StreamWriter _csv;
    private readonly System.Globalization.CultureInfo _inv =
        System.Globalization.CultureInfo.InvariantCulture;

    private long _samplesSinceReport;
    private long _totalSamples;
    private bool _disposed;

    public SnrCsvSink(
        IAudioSink inner,
        string csvPath,
        int sampleRate,
        SnrEstimator snr,
        int reportIntervalMs = 100)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _snr = snr ?? throw new ArgumentNullException(nameof(snr));
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (reportIntervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(reportIntervalMs));
        if (string.IsNullOrWhiteSpace(csvPath)) throw new ArgumentException("csvPath required.", nameof(csvPath));

        _sampleRate = sampleRate;
        _reportIntervalSamples = (long)sampleRate * reportIntervalMs / 1000L;

        var dir = Path.GetDirectoryName(csvPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _csv = new StreamWriter(csvPath, append: false);
        _csv.WriteLine("time_s,snr_db");
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _inner.Write(samples);
        _samplesSinceReport += samples.Length;
        _totalSamples += samples.Length;

        // Observe every write so LastDb tracks the latest block
        _snr.Observe(samples);

        while (_samplesSinceReport >= _reportIntervalSamples)
        {
            _samplesSinceReport -= _reportIntervalSamples;
            double t = _totalSamples / (double)_sampleRate;
            _csv.Write(t.ToString("F3", _inv));
            _csv.Write(',');
            _csv.WriteLine(_snr.LastDb.ToString("F2", _inv));
        }
    }

    public void Flush()
    {
        _csv.Flush();
        _inner.Flush();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _csv.Dispose(); }
        finally { _inner.Dispose(); }
    }
}
