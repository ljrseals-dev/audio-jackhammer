using Denoiser.Models.DeepFilterNet;
using Xunit;

namespace Denoiser.Tests;

public sealed class StftRoundTripTests
{
    private const int HopSize = 480;

    [Fact]
    public void ConstantInput_ReconstructsConstant_AfterWarmup()
    {
        var stft = new DeepFilterNetStft(new FftSharpBackend(960));
        var constant = new float[HopSize];
        Array.Fill(constant, 0.5f);
        var output = new float[HopSize];

        for (int f = 0; f < 20; f++)
        {
            var spectrum = stft.Analyze(constant);
            stft.Synthesize(spectrum, output);

            if (f < 2) continue;
            for (int i = 0; i < HopSize; i++)
                Assert.InRange(output[i], 0.49f, 0.51f);
        }
    }

    [Fact]
    public void SineWave_ReconstructsWithoutBoundaryClicks()
    {
        var stft = new DeepFilterNetStft(new FftSharpBackend(960));
        const int SampleRate = 48000;
        const float Freq = 1000f;

        var input = new float[HopSize];
        var output = new float[HopSize];
        var allOutput = new List<float>();

        for (int f = 0; f < 40; f++)
        {
            for (int i = 0; i < HopSize; i++)
                input[i] = 0.5f * MathF.Sin(2 * MathF.PI * Freq * (f * HopSize + i) / SampleRate);

            var spectrum = stft.Analyze(input);
            stft.Synthesize(spectrum, output);
            if (f >= 2) allOutput.AddRange((float[])output.Clone());
        }

        for (int i = 1; i < allOutput.Count; i++)
        {
            float delta = Math.Abs(allOutput[i] - allOutput[i - 1]);
            Assert.True(delta < 0.1f,
                $"Discontinuity at output[{i}]: delta={delta:F4}. STFT OLA not accumulating.");
        }
    }
}
