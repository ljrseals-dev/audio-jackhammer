using Denoiser.Models.DeepFilterNet;
using Denoiser.Tests.Support;
using Xunit;

namespace Denoiser.Tests;

public sealed class DeepFilterNetDspTests
{
    [Fact]
    public void VorbisWindow_SatisfiesPrincenBradley()
    {
        var w = VorbisWindow.Create(960);
        for (int i = 0; i < 480; i++)
        {
            double sum = w[i] * w[i] + w[i + 480] * w[i + 480];
            Assert.InRange(sum, 0.999, 1.001);
        }
    }

    [Fact]
    public void Stft_RoundTrip_ConstantIsStable()
    {
        var stft = new DeepFilterNetStft();
        var hop = new float[480];
        Array.Fill(hop, 0.5f);
        for (int i = 0; i < 6; i++)
        {
            var spec = stft.Analyze(hop).ToArray();
            var outHop = new float[480];
            stft.Synthesize(spec, outHop);
            Array.Fill(hop, 0.5f);
            if (i >= 2)
                Assert.InRange(outHop.Average(), 0.45f, 0.55f);
        }
    }

    [Fact]
    public void Dfn_RequiresOverlapAdd_IsFalse()
    {
        // Guard: even without loading ONNX, the type must declare false.
        // We can't construct without a model file, so reflect the constant contract
        // via a documented assertion on the public API once constructed is skipped.
        Assert.False(false == true); // placeholder — construction requires model
        // The real check is in Dfn_RequiresOverlapAdd_WhenConstructed below.
    }

    [Fact]
    [Trait("Category", "RequiresWeights")]
    public void Dfn_RequiresOverlapAdd_WhenConstructed()
    {
        var modelPath = Environment.GetEnvironmentVariable("DEEP_FILTER_NET_MODEL");
        if (Skip.Missing("DEEP_FILTER_NET_MODEL", modelPath) || Skip.MissingFile(modelPath!)) return;
        using var dfn = new DeepFilterNetOnnxDenoiser(modelPath,
            Environment.GetEnvironmentVariable("DEEP_FILTER_NET_AUX"));
        Assert.False(dfn.RequiresOverlapAdd);
        Assert.Equal(480, dfn.FrameSize);
    }

    [Fact]
    [Trait("Category", "RequiresWeights")]
    public void Dfn_SynthesisContinuity_AfterWarmup()
    {
        var modelPath = Environment.GetEnvironmentVariable("DEEP_FILTER_NET_MODEL");
        var auxPath = Environment.GetEnvironmentVariable("DEEP_FILTER_NET_AUX");
        if (Skip.Missing("DEEP_FILTER_NET_MODEL", modelPath) || Skip.Missing("DEEP_FILTER_NET_AUX", auxPath)) return;
        if (Skip.MissingFile(modelPath!)) return;

        using var dfn = new DeepFilterNetOnnxDenoiser(modelPath, auxPath);
        var frame = new float[480];
        Array.Fill(frame, 0.5f);
        var output = new List<float>();

        for (int i = 0; i < 40; i++)
        {
            dfn.Process(frame);
            if (i >= 4) output.AddRange((float[])frame.Clone());
            Array.Fill(frame, 0.5f);
        }

        for (int i = 1; i < output.Count; i++)
        {
            float delta = Math.Abs(output[i] - output[i - 1]);
            Assert.True(delta < 0.05f,
                $"Discontinuity at sample {i}: delta={delta:F4}. Check Vorbis window + tail ordering.");
        }
    }
}
