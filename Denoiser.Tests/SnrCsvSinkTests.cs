using Denoiser.Core;
using Xunit;

namespace Denoiser.Tests;

public sealed class SnrCsvSinkTests
{
    private sealed class CollectingSink : IAudioSink
    {
        public long TotalSamples;
        public int Flushes;
        public bool Disposed;

        public void Write(ReadOnlySpan<float> s) => TotalSamples += s.Length;
        public void Flush() => Flushes++;
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void Write_ProducesHeaderAndRows()
    {
        var path = Path.GetTempFileName();
        try
        {
            var inner = new CollectingSink();
            var snr = new SnrEstimator();
            snr.Observe(new float[480]);

            using (var sink = new SnrCsvSink(inner, path, 48000, snr, reportIntervalMs: 10))
            {
                for (int i = 0; i < 5; i++)
                    sink.Write(new float[480]);
                sink.Flush();
            }

            var lines = File.ReadAllLines(path);
            Assert.Equal("time_s,snr_db", lines[0]);
            Assert.True(lines.Length >= 2, "Expected at least one data row.");

            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                Assert.Equal(2, parts.Length);
                Assert.True(double.TryParse(parts[0],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var t));
                Assert.True(t >= 0);
                Assert.True(double.TryParse(parts[1],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out _));
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Write_ForwardsToInner()
    {
        var path = Path.GetTempFileName();
        try
        {
            var inner = new CollectingSink();
            using (var sink = new SnrCsvSink(inner, path, 48000, new SnrEstimator()))
            {
                sink.Write(new float[1000]);
                sink.Write(new float[500]);
            }
            Assert.Equal(1500, inner.TotalSamples);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Dispose_ClosesBothCsvAndInner()
    {
        var path = Path.GetTempFileName();
        try
        {
            var inner = new CollectingSink();
            var sink = new SnrCsvSink(inner, path, 48000, new SnrEstimator());
            sink.Dispose();
            Assert.True(inner.Disposed);
            Assert.Throws<ObjectDisposedException>(() => sink.Write(new float[10]));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var path = Path.GetTempFileName();
        try
        {
            var sink = new SnrCsvSink(new CollectingSink(), path, 48000, new SnrEstimator());
            sink.Dispose();
            sink.Dispose();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Cadence_MatchesInterval()
    {
        var path = Path.GetTempFileName();
        try
        {
            var inner = new CollectingSink();
            using (var sink = new SnrCsvSink(inner, path, 48000, new SnrEstimator(),
                                             reportIntervalMs: 100))
            {
                for (int i = 0; i < 50; i++)
                    sink.Write(new float[480]);
                sink.Flush();
            }

            var lines = File.ReadAllLines(path);
            Assert.Equal(6, lines.Length); // header + 5 rows for 500 ms / 100 ms
        }
        finally { File.Delete(path); }
    }
}
