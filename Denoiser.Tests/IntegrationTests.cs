using Denoiser.Codecs;
using Denoiser.Core;
using Xunit;

namespace Denoiser.Tests;

public sealed class IntegrationTests
{
    [Fact]
    public void OlaPlusLimiterPlusSnrCsv_EndToEnd()
    {
        var csvPath = Path.GetTempFileName();
        var wavPath = Path.Combine(Path.GetTempPath(), $"int_{Guid.NewGuid():N}.wav");
        try
        {
            const int frameSize = 480;
            var inner = new IdentityModel(frameSize);
            using var ola = new OverlapAddAdapter(inner);
            var limiter = new SoftLimiter(lookAhead: true, sampleRate: 48000,
                                          lookAheadMs: 5f, releaseMs: 100f);
            var snr = new SnrEstimator();

            using var wavSink = new WavFileSink(wavPath, 48000);
            using var sink = new SnrCsvSink(wavSink, csvPath, 48000, snr, reportIntervalMs: 100);

            int totalSamples = 48000 * 2;
            var input = new float[totalSamples];
            for (int i = 0; i < totalSamples; i++)
                input[i] = 0.5f * MathF.Sin(2 * MathF.PI * 440f * i / 48000f);

            var framer = new FrameAccumulator(frameSize);
            for (int i = 0; i < totalSamples; i += 8192)
            {
                var chunk = input.AsSpan(i, Math.Min(8192, totalSamples - i));
                framer.Push(chunk, (frame, length) =>
                {
                    var span = frame.AsSpan(0, length);
                    ola.Process(span);
                    limiter.Process(span);
                    snr.Observe(span);
                    sink.Write(span);
                });
            }
            framer.Flush((frame, length) =>
            {
                var span = frame.AsSpan(0, length);
                ola.Process(span);
                limiter.Process(span);
                snr.Observe(span);
                sink.Write(span);
            });

            var tail = new float[frameSize];
            int n = ola.Flush(tail);
            if (n > 0)
            {
                var ts = tail.AsSpan(0, n);
                limiter.Process(ts);
                snr.Observe(ts);
                sink.Write(ts);
            }
            sink.Flush();

            Assert.True(File.Exists(wavPath));
            Assert.True(new FileInfo(wavPath).Length > 44, "WAV must have data.");

            var csvLines = File.ReadAllLines(csvPath);
            Assert.True(csvLines.Length >= 2, "CSV must have at least one row.");
            Assert.Equal("time_s,snr_db", csvLines[0]);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
            if (File.Exists(wavPath)) File.Delete(wavPath);
        }
    }

    private sealed class IdentityModel : IDenoiser
    {
        private readonly float[] _analysisWindow;
        public IdentityModel(int frameSize)
        {
            FrameSize = frameSize;
            _analysisWindow = new float[frameSize];
            for (int i = 0; i < frameSize; i++)
            {
                double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / frameSize));
                _analysisWindow[i] = (float)Math.Sqrt(hann);
            }
        }
        public int FrameSize { get; }
        public int SampleRate => 48000;
        public string Name => "identity";
        public bool RequiresOverlapAdd => false;
        public void Process(Span<float> frame)
        {
            for (int i = 0; i < frame.Length; i++)
                frame[i] *= _analysisWindow[i];
        }
        public void Dispose() { }
    }
}
