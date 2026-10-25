using Denoiser.Models.DeepFilterNet;
using Xunit;

namespace Denoiser.Tests;

public sealed class StftBackendParityTests
{
    [Fact]
    public void NaiveAndFftSharp_ProduceIdenticalSpectrum()
    {
        var naive = new DeepFilterNetStft(new NaiveDftBackend(960));
        var fast = new DeepFilterNetStft(new FftSharpBackend(960));

        var input = new float[480];
        var rng = new Random(42);
        for (int i = 0; i < 480; i++) input[i] = (float)(rng.NextDouble() * 2 - 1);

        for (int f = 0; f < 5; f++)
        {
            var a = naive.Analyze(input);
            var b = fast.Analyze(input);
            for (int k = 0; k < 481; k++)
            {
                Assert.Equal(a[k].Real, b[k].Real, 4);
                Assert.Equal(a[k].Imaginary, b[k].Imaginary, 4);
            }
        }
    }

    [Fact]
    public void NaiveAndFftSharp_ProduceIdenticalSynthesis()
    {
        var naive = new DeepFilterNetStft(new NaiveDftBackend(960));
        var fast = new DeepFilterNetStft(new FftSharpBackend(960));

        var input = new float[480];
        for (int i = 0; i < 480; i++)
            input[i] = 0.5f * MathF.Sin(2 * MathF.PI * 1000 * i / 48000f);

        for (int f = 0; f < 5; f++)
        {
            var a = naive.Analyze(input);
            var b = fast.Analyze(input);
            var outA = new float[480];
            var outB = new float[480];
            naive.Synthesize(a, outA);
            fast.Synthesize(b, outB);
            for (int i = 0; i < 480; i++)
                Assert.Equal(outA[i], outB[i], 4);
        }
    }
}
