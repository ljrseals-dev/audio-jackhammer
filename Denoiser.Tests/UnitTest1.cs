using Denoiser.Codecs;
using Denoiser.Core;
using Xunit;

namespace Denoiser.Tests;

public class NullDenoiserTests
{
    [Fact]
    public void NullDenoiser_PreservesSamples_BitExact()
    {
        var denoiser = new NullDenoiser();
        var original = new float[480];
        for (int i = 0; i < original.Length; i++)
            original[i] = (float)Math.Sin(2 * Math.PI * 440 * i / 48000.0);

        var copy = (float[])original.Clone();
        denoiser.Process(copy);

        Assert.Equal(original, copy);
    }
}

public class HighPassFilterTests
{
    [Fact]
    public void HighPass_AttenuatesDcAndLowFrequencies()
    {
        var filter = new HighPassFilter(90f, 48000);
        // DC signal should be heavily attenuated
        var dc = new float[4800];
        Array.Fill(dc, 1.0f);
        filter.Process(dc);

        float rms = (float)Math.Sqrt(dc.Select(s => s * s).Average());
        Assert.True(rms < 0.1f, $"DC RMS after HPF should be near zero, got {rms}");
    }

    [Fact]
    public void HighPass_PreservesHighFrequencyTone()
    {
        var filter = new HighPassFilter(90f, 48000);
        var tone = new float[4800];
        for (int i = 0; i < tone.Length; i++)
            tone[i] = (float)Math.Sin(2 * Math.PI * 1000 * i / 48000.0); // 1 kHz

        float rmsBefore = (float)Math.Sqrt(tone.Select(s => s * s).Average());
        filter.Process(tone);
        // Skip transient
        float rmsAfter = (float)Math.Sqrt(tone.Skip(480).Select(s => s * s).Average());

        Assert.True(rmsAfter > rmsBefore * 0.7f,
            $"1 kHz tone should largely survive HPF: before={rmsBefore}, after={rmsAfter}");
    }
}

public class PipelineTests
{
    [Fact]
    public void Pipeline_EndToEnd_SyntheticTone()
    {
        // 2-second 440 Hz tone @ 48 kHz mono
        const int sampleRate = 48000;
        const int durationSec = 2;
        int totalSamples = sampleRate * durationSec;
        var pcm = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
            pcm[i] = 0.3f * (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);

        string tmpIn = Path.Combine(Path.GetTempPath(), $"denoiser_test_in_{Guid.NewGuid():N}.wav");
        string tmpOut = Path.Combine(Path.GetTempPath(), $"denoiser_test_out_{Guid.NewGuid():N}.wav");

        try
        {
            // Write input WAV via NAudio
            using (var writer = new NAudio.Wave.WaveFileWriter(tmpIn,
                       NAudio.Wave.WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)))
            {
                writer.WriteSamples(pcm, 0, pcm.Length);
            }

            using var source = new AudioFileSource(tmpIn);
            using var sink = new WavFileSink(tmpOut);
            using var denoiser = new NullDenoiser();
            using var pipeline = new DenoiserPipeline(source, denoiser, sink, highPassHz: 90f, enableHighPass: true);

            var (processed, elapsed) = pipeline.Run();
            Assert.True(processed > totalSamples * 0.9, $"Expected ~{totalSamples} samples, got {processed}");
            Assert.True(File.Exists(tmpOut));
            Assert.True(new FileInfo(tmpOut).Length > 1000);
        }
        finally
        {
            if (File.Exists(tmpIn)) File.Delete(tmpIn);
            if (File.Exists(tmpOut)) File.Delete(tmpOut);
        }
    }
}

public class SnrEstimatorTests
{
    [Fact]
    public void SnrEstimator_ReturnsReasonableValue()
    {
        var samples = new float[4800];
        var rng = new Random(42);
        for (int i = 0; i < samples.Length; i++)
            samples[i] = (float)(rng.NextDouble() * 0.1 - 0.05); // noise
        // Inject a louder tone in the middle
        for (int i = 2000; i < 2800; i++)
            samples[i] += 0.5f * (float)Math.Sin(2 * Math.PI * 500 * i / 48000.0);

        float snr = SnrEstimator.EstimateDb(samples);
        Assert.True(snr > 0, $"Expected positive SNR estimate, got {snr}");
    }
}
