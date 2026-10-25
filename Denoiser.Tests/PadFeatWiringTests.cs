using Denoiser.Models.DeepFilterNet;
using Denoiser.Tests.Support;
using Xunit;

namespace Denoiser.Tests;

public sealed class PadFeatWiringTests
{
    [Fact]
    public void Pipeline_AppliesPadFeat_BeforeOnnx()
    {
        var session = new RecordingOnnxSession();
        var denoiser = new DeepFilterNetOnnxDenoiser(
            session, new StubErbFilterbank(), new DeepFilterKernelImpl());

        var frame = new float[480];
        for (int f = 0; f < 6; f++)
        {
            Array.Fill(frame, f + 1);
            denoiser.Process(frame);
        }

        var destination = new float[480 * 20];
        denoiser.Flush(destination);

        Assert.True(session.Calls.Count > 0, "ONNX Run was never called.");

        // Last call should have t>=6; last 2 frames of padded ERB features must be zero
        var (featErb, _, t) = session.Calls[^1];
        Assert.True(t >= 6, $"Expected t >= 6, got {t}.");

        for (int f = t - 2; f < t; f++)
        {
            for (int b = 0; b < 32; b++)
                Assert.Equal(0f, featErb[f * 32 + b]);
        }
    }
}
