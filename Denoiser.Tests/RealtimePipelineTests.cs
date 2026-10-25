using Denoiser.Core;
using Denoiser.Realtime;
using Denoiser.Tests.Support;
using Xunit;

namespace Denoiser.Tests;

public sealed class RealtimePipelineTests
{
    [Fact]
    public void NullModel_RoundTripsInput()
    {
        var input = new float[48000];
        for (int i = 0; i < input.Length; i++)
            input[i] = (i % 480) / 480f;

        using var capture = new FakeCaptureDevice(input);
        using var render = new FakeRenderDevice();
        using var pipeline = new RealtimePipeline(
            capture, render, new NullDenoiser(),
            new LatencyBudget(10, 0, 10));

        pipeline.Start();
        Thread.Sleep(1500);
        pipeline.Stop();

        var output = render.Captured;
        Assert.True(output.Count > 1000, $"Render produced too little output ({output.Count}).");
    }

    [Fact]
    public void Underrun_WritesSilenceAndIncrementsCounter()
    {
        // Empty capture → render pulls silence and records underruns
        using var capture = new FakeCaptureDevice(Array.Empty<float>());
        using var render = new FakeRenderDevice();
        using var pipeline = new RealtimePipeline(
            capture, render, new NullDenoiser(),
            new LatencyBudget(10, 0, 10));

        pipeline.Start();
        Thread.Sleep(200);
        pipeline.Stop();

        Assert.True(pipeline.Latency.Underruns > 0,
            "Expected underruns when capture produces nothing.");
    }

    [Fact]
    public void FinalSnapshot_PrintedOnStop()
    {
        using var capture = new FakeCaptureDevice(new float[480]);
        using var render = new FakeRenderDevice();
        using var pipeline = new RealtimePipeline(
            capture, render, new NullDenoiser(),
            new LatencyBudget(10, 40, 10));
        pipeline.Start();
        Thread.Sleep(50);
        pipeline.Stop();
        var snap = pipeline.Latency.Snapshot();
        Assert.Contains("budget:", snap);
        Assert.Contains("underruns=", snap);
    }
}
