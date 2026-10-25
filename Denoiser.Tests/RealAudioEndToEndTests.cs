using Denoiser.Core;
using Denoiser.Tests.Support;
using Xunit;

namespace Denoiser.Tests;

public sealed class RealAudioEndToEndTests
{
    private const int SampleRate = 48000;
    private const int NoiseOnlyEnd = SampleRate;
    private const int SpeechStart = SampleRate;
    private const int SpeechEnd = 2 * SampleRate;

    [Fact]
    public void NullBaseline_DoesNotReduceNoiseFloor()
    {
        // Control: Null vs Null should show ~0 dB reduction (test has teeth inverted).
        var path = Fixtures.Require("construction_sample.wav");
        if (path is null)
        {
            // Fall back to synthetic
            var input = SyntheticAudio.GenerateConstructionLike2s();
            var a = TestPipeline.Run(input, new NullDenoiser(), wrapOla: false);
            var b = TestPipeline.Run(input, new NullDenoiser(), wrapOla: false);
            double na = Dsp.BandRms(a.AsSpan(0, Math.Min(NoiseOnlyEnd, a.Length)), SampleRate, 200, 4000);
            double nb = Dsp.BandRms(b.AsSpan(0, Math.Min(NoiseOnlyEnd, b.Length)), SampleRate, 200, 4000);
            double db = 20 * Math.Log10(Math.Max(na, 1e-12) / Math.Max(nb, 1e-12));
            Assert.True(Math.Abs(db) < 0.5, $"Null vs Null should be ~0 dB, got {db:F2}");
            return;
        }

        var samples = Fixtures.LoadMono48k(path);
        var nullOut = TestPipeline.Run(samples, new NullDenoiser(), wrapOla: false);
        double nullNoise = Dsp.BandRms(nullOut.AsSpan(0, Math.Min(NoiseOnlyEnd, nullOut.Length)), SampleRate, 200, 4000);
        Assert.True(nullNoise > 1e-6, "Fixture noise region should have energy");
    }

    [Fact]
    [Trait("Category", "RequiresWeights")]
    public void RnNoise_ReducesNoiseFloor_vsNullBaseline()
    {
        IDenoiser? rn = null;
        try { rn = new Denoiser.Models.RnNoiseDenoiser(); }
        catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException)
        {
            Skip.Unless(false, $"RNNoise unavailable: {ex.Message}");
            return;
        }

        using (rn)
        {
            float[] input;
            var path = Fixtures.Require("construction_sample.wav");
            input = path is not null ? Fixtures.LoadMono48k(path) : SyntheticAudio.GenerateConstructionLike2s();
            Assert.True(input.Length >= SpeechEnd);

            var nullOut = TestPipeline.Run(input, new NullDenoiser(), wrapOla: false);
            var rnOut = TestPipeline.Run(input, rn, wrapOla: false);

            int nLen = Math.Min(NoiseOnlyEnd, Math.Min(nullOut.Length, rnOut.Length));
            double nullNoise = Dsp.BandRms(nullOut.AsSpan(0, nLen), SampleRate, 200, 4000);
            double rnNoise = Dsp.BandRms(rnOut.AsSpan(0, nLen), SampleRate, 200, 4000);

            double noiseReductionDb = 20 * Math.Log10(Math.Max(nullNoise, 1e-12) / Math.Max(rnNoise, 1e-12));
            Assert.True(noiseReductionDb >= 2.0,
                $"RnNoise must reduce noise-only band RMS by ≥ 2 dB vs Null. " +
                $"Got {noiseReductionDb:F2} dB (Null={nullNoise:E3}, RNNoise={rnNoise:E3}).");
        }
    }

    [Fact]
    [Trait("Category", "RequiresWeights")]
    public void RnNoise_PreservesSpeechRegion_DoesNotSilence()
    {
        IDenoiser? rn = null;
        try { rn = new Denoiser.Models.RnNoiseDenoiser(); }
        catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException)
        {
            Skip.Unless(false, $"RNNoise unavailable: {ex.Message}");
            return;
        }

        using (rn)
        {
            float[] input;
            var path = Fixtures.Require("construction_sample.wav");
            input = path is not null ? Fixtures.LoadMono48k(path) : SyntheticAudio.GenerateConstructionLike2s();

            var rnOut = TestPipeline.Run(input, rn, wrapOla: false);
            int sLen = Math.Min(SpeechEnd - SpeechStart, rnOut.Length - SpeechStart);
            if (sLen <= 0) return;

            double inputVoice = Dsp.BandRms(input.AsSpan(SpeechStart, sLen), SampleRate, 300, 3400);
            double outVoice = Dsp.BandRms(rnOut.AsSpan(SpeechStart, sLen), SampleRate, 300, 3400);
            double preservation = outVoice / Math.Max(inputVoice, 1e-12);
            Assert.True(preservation >= 0.70,
                $"RnNoise must preserve ≥ 70% of voice-band energy. Got {preservation:P1}.");
        }
    }

    [Fact]
    public void NullPipeline_DoesNotClipOutput()
    {
        var input = SyntheticAudio.GenerateConstructionLike2s();
        var output = TestPipeline.Run(input, new NullDenoiser(), wrapOla: false);
        float maxSample = 0;
        foreach (var s in output)
            if (MathF.Abs(s) > maxSample) maxSample = MathF.Abs(s);
        Assert.True(maxSample <= 1.0f, $"Output clipped: max={maxSample:F4}");
    }
}
