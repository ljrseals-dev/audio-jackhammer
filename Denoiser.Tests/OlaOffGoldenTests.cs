using Denoiser.Core;
using Denoiser.Tests.Support;
using Xunit;

namespace Denoiser.Tests;

public sealed class OlaOffGoldenTests
{
    private const double Tolerance = 1e-5;

    [Fact]
    public void Null_OlaOff_IsDeterministic_AcrossRuns()
    {
        var out1 = TestPipeline.Run(
            SyntheticAudio.Generate2s440_660_Seed42(),
            new NullDenoiser(), enableHighPass: true, lookAhead: false, wrapOla: false);
        var out2 = TestPipeline.Run(
            SyntheticAudio.Generate2s440_660_Seed42(),
            new NullDenoiser(), enableHighPass: true, lookAhead: false, wrapOla: false);
        var (maxDiff, _) = Dsp.CompareSamples(out1, out2);
        Assert.True(maxDiff <= Tolerance, $"Pipeline is not deterministic: maxDiff={maxDiff}");
    }

    [Fact]
    public void Null_OlaOff_MatchesGolden()
    {
        // Generate golden on first run if missing, then compare.
        var goldenPath = Fixtures.PathOf("ola_off_golden_null.wav");
        var input = SyntheticAudio.Generate2s440_660_Seed42();
        var output = TestPipeline.Run(input, new NullDenoiser(),
            enableHighPass: true, lookAhead: false, wrapOla: false);

        if (!File.Exists(goldenPath))
        {
            Fixtures.WriteWav(goldenPath, output);
            Console.WriteLine($"[INFO] Wrote golden {goldenPath} — commit it.");
        }

        var golden = Fixtures.LoadMono48k(goldenPath);
        int len = Math.Min(golden.Length, output.Length);
        var (maxDiff, firstIdx) = Dsp.CompareSamples(
            golden.AsSpan(0, len), output.AsSpan(0, len));

        if (maxDiff > Tolerance)
        {
            Assert.Fail(
                $"OlaOff output diverged from golden.\n" +
                $"  max abs diff: {maxDiff:E3} at sample {firstIdx}\n" +
                $"  golden length: {golden.Length}, output length: {output.Length}\n" +
                $"  If intentional, delete the golden and re-run to regenerate.");
        }
    }

    [Fact]
    [Trait("Category", "RequiresWeights")]
    public void RnNoise_OlaOff_MatchesGolden_WhenAvailable()
    {
        var goldenPath = Fixtures.Require("ola_off_golden_rnnoise.wav");
        if (goldenPath is null) return;

        IDenoiser model;
        try { model = new Denoiser.Models.RnNoiseDenoiser(); }
        catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException)
        {
            Skip.Unless(false, $"RNNoise unavailable: {ex.Message}");
            return;
        }

        using (model)
        {
            var input = SyntheticAudio.Generate2s440_660_Seed42();
            var output = TestPipeline.Run(input, model, enableHighPass: true, lookAhead: false, wrapOla: false);
            var golden = Fixtures.LoadMono48k(goldenPath);
            int len = Math.Min(golden.Length, output.Length);
            var (maxDiff, firstIdx) = Dsp.CompareSamples(
                golden.AsSpan(0, len), output.AsSpan(0, len));
            Assert.True(maxDiff <= Tolerance,
                $"RNNoise golden diverged: maxDiff={maxDiff:E3} at {firstIdx}");
        }
    }
}
