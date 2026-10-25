using Denoiser.Codecs;
using Denoiser.Core;
using NAudio.Wave;
using Xunit;

namespace Denoiser.Tests;

/// <summary>
/// Verifies that non-multiple-of-FrameSize inputs produce padded output of the
/// expected length and that consecutive frame boundaries are continuous within
/// the soft-limiter ceiling (no hard discontinuities from framing errors).
/// </summary>
public class PipelineFrameBoundaryTests
{
    private const int FrameSize = 480;
    private const int SampleRate = 48000;

    [Theory]
    [InlineData(1)]
    [InlineData(479)]
    [InlineData(481)]
    [InlineData(1000)]
    [InlineData(4097)]
    public void NonMultipleLength_ProducesPaddedOutput(int sampleCount)
    {
        string tmpIn = Path.Combine(Path.GetTempPath(), $"pfb_in_{Guid.NewGuid():N}.wav");
        string tmpOut = Path.Combine(Path.GetTempPath(), $"pfb_out_{Guid.NewGuid():N}.wav");

        try
        {
            // Write a short ramp (non-silent so padding is distinguishable)
            var pcm = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                pcm[i] = (i % 100) / 100f * 0.2f;

            using (var writer = new WaveFileWriter(tmpIn, WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1)))
                writer.WriteSamples(pcm, 0, pcm.Length);

            using var source = new AudioFileSource(tmpIn);
            using var sink = new WavFileSink(tmpOut, float32: true);
            using var denoiser = new NullDenoiser();
            // Disable HPF so we can inspect sample continuity without filter transients
            using var pipeline = new DenoiserPipeline(source, denoiser, sink, enableHighPass: false);

            var (processed, _) = pipeline.Run();

            int expected = ((sampleCount + FrameSize - 1) / FrameSize) * FrameSize;
            Assert.Equal(expected, processed);

            // Read output back and verify length
            using var reader = new AudioFileReader(tmpOut);
            var outBuf = new float[expected + 100];
            int got = reader.Read(outBuf, 0, outBuf.Length);
            // AudioFileReader may report slightly different due to format; check file exists and size > 0
            Assert.True(new FileInfo(tmpOut).Length > 44);
            Assert.True(got >= sampleCount); // at least the original content
        }
        finally
        {
            if (File.Exists(tmpIn)) File.Delete(tmpIn);
            if (File.Exists(tmpOut)) File.Delete(tmpOut);
        }
    }

    [Fact]
    public void FrameBoundaries_NoHardDiscontinuities()
    {
        // 2.5 frames of a smooth 440 Hz sine — should remain continuous at boundaries
        // with NullDenoiser + no HPF + soft limiter.
        int sampleCount = FrameSize * 2 + FrameSize / 2; // 1200
        string tmpIn = Path.Combine(Path.GetTempPath(), $"pfb_sine_in_{Guid.NewGuid():N}.wav");
        string tmpOut = Path.Combine(Path.GetTempPath(), $"pfb_sine_out_{Guid.NewGuid():N}.wav");

        try
        {
            var pcm = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                pcm[i] = 0.3f * (float)Math.Sin(2 * Math.PI * 440 * i / SampleRate);

            using (var writer = new WaveFileWriter(tmpIn, WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1)))
                writer.WriteSamples(pcm, 0, pcm.Length);

            using var source = new AudioFileSource(tmpIn);
            // Capture sink to inspect samples in memory
            var captured = new List<float>();
            using var memSink = new CapturingSink(captured);
            using var denoiser = new NullDenoiser();
            using var pipeline = new DenoiserPipeline(source, denoiser, memSink, enableHighPass: false);

            var (processed, _) = pipeline.Run();
            Assert.Equal(((sampleCount + FrameSize - 1) / FrameSize) * FrameSize, processed);
            Assert.Equal(processed, captured.Count);

            // At each frame boundary, adjacent samples must not jump by more than the
            // soft-limiter ceiling (0.95). For a continuous sine they should be far closer.
            for (int b = FrameSize; b < captured.Count; b += FrameSize)
            {
                float delta = Math.Abs(captured[b] - captured[b - 1]);
                Assert.True(delta < 0.95f,
                    $"Hard discontinuity at sample {b}: delta={delta}");
            }

            // For the non-padded region, output should match input closely (NullDenoiser)
            for (int i = 0; i < sampleCount; i++)
            {
                Assert.True(Math.Abs(captured[i] - pcm[i]) < 1e-5f,
                    $"Sample {i} diverged: in={pcm[i]}, out={captured[i]}");
            }
        }
        finally
        {
            if (File.Exists(tmpIn)) File.Delete(tmpIn);
            if (File.Exists(tmpOut)) File.Delete(tmpOut);
        }
    }

    /// <summary>In-memory sink for boundary inspection.</summary>
    private sealed class CapturingSink : IAudioSink
    {
        private readonly List<float> _samples;
        public CapturingSink(List<float> samples) => _samples = samples;
        public void Write(ReadOnlySpan<float> samples)
        {
            foreach (var s in samples) _samples.Add(s);
        }
        public void Flush() { }
        public void Dispose() { }
    }
}
