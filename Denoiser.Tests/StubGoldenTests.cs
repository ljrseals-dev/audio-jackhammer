using Denoiser.Core;
using Denoiser.Tests.Support;
using Xunit;

namespace Denoiser.Tests;

public sealed class StubGoldenTests
{
    private const double Tolerance = 1e-5;

    [Fact]
    public void Stub_OlaOff_IsDeterministic_AcrossRuns()
    {
        var input = SyntheticAudio.Generate2s440_660_Seed42();
        var out1 = TestPipeline.Run(input, new DeterministicStubModel(), wrapOla: false);
        var out2 = TestPipeline.Run(input, new DeterministicStubModel(), wrapOla: false);
        var (maxDiff, _) = Dsp.CompareSamples(out1, out2);
        Assert.True(maxDiff <= Tolerance, $"Stub path not deterministic: {maxDiff}");
    }

    [Fact]
    public void Stub_OlaOff_MatchesGolden()
    {
        var input = SyntheticAudio.Generate2s440_660_Seed42();
        var output = TestPipeline.Run(input, new DeterministicStubModel(), wrapOla: false);

        var goldenPath = Fixtures.PathOf("ola_off_golden_stub.wav");
        if (!File.Exists(goldenPath))
        {
            Fixtures.WriteWav(goldenPath, output);
            Console.WriteLine($"[INFO] Wrote stub golden {goldenPath} — commit it.");
        }

        var golden = Fixtures.LoadMono48k(goldenPath);
        int len = Math.Min(golden.Length, output.Length);
        var (maxDiff, firstIdx) = Dsp.CompareSamples(golden.AsSpan(0, len), output.AsSpan(0, len));
        if (maxDiff > Tolerance)
        {
            Assert.Fail(
                $"Stub handoff diverged from golden.\n" +
                $"  max abs diff: {maxDiff:E3} at sample {firstIdx}\n" +
                $"  This means FrameAccumulator/pipeline handoff changed sample content or boundaries.");
        }
    }

    [Fact]
    public void Stub_DetectsFrameBoundaryShift()
    {
        // Sanity: if we process with wrong frame size the stub throws.
        var stub = new DeterministicStubModel();
        Assert.Throws<ArgumentException>(() => stub.Process(new float[481]));
    }
}
