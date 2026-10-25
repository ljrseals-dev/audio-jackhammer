using Denoiser.Models.DeepFilterNet;
using Denoiser.Tests.Support;
using Xunit;

namespace Denoiser.Tests;

public sealed class DfnModeTests
{
    [Fact]
    public void MaskOnly_SkipsKernel_Full_CallsKernel()
    {
        var session = new FakeOnnxSession();
        var spyFull = new SpyKernel(new DeepFilterKernelImpl());
        var dfnFull = new DeepFilterNetOnnxDenoiser(session, new StubErbFilterbank(), spyFull, mode: DfnMode.Full);

        var frame = new float[480];
        Array.Fill(frame, 0.25f);
        for (int i = 0; i < 8; i++)
            dfnFull.Process(frame);

        Assert.True(spyFull.ReceivedWindows.Count > 0, "Full mode must invoke the deep-filter kernel.");

        var spyMask = new SpyKernel(new DeepFilterKernelImpl());
        var dfnMask = new DeepFilterNetOnnxDenoiser(
            new FakeOnnxSession(), new StubErbFilterbank(), spyMask, mode: DfnMode.MaskOnly);
        Array.Fill(frame, 0.25f);
        for (int i = 0; i < 8; i++)
            dfnMask.Process(frame);

        Assert.Equal(0, spyMask.ReceivedWindows.Count);
    }
}
