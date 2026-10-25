using Denoiser.Models.DeepFilterNet;
using Denoiser.Tests.Support;
using Xunit;

namespace Denoiser.Tests;

public sealed class EmissionOrderTests
{
    [Fact]
    public void EmissionQueue_FeedsFramesInTimeOrder()
    {
        var session = new FakeOnnxSession();
        var spy = new SpyKernel(new DeepFilterKernelImpl());
        var erb = new StubErbFilterbank();
        var denoiser = new DeepFilterNetOnnxDenoiser(session, erb, spy);

        var frame = new float[480];
        for (int f = 0; f < 10; f++)
        {
            Array.Fill(frame, f + 1);
            denoiser.Process(frame);
        }

        var destination = new float[480 * 20];
        denoiser.Flush(destination);

        Assert.True(spy.ReceivedWindows.Count >= 1,
            "Spy saw no kernel calls. Emission queue did not trigger.");

        var first = spy.ReceivedWindows[0];
        var dc = new float[5];
        for (int n = 0; n < 5; n++)
            dc[n] = (float)first[n * 481].Real;

        for (int n = 1; n < 5; n++)
        {
            Assert.True(dc[n] > dc[n - 1],
                $"Emission window not time-ordered. Got DC=[{string.Join(", ", dc)}]. " +
                "Expected increasing. If decreasing, queue was reversed.");
        }

        if (spy.ReceivedWindows.Count >= 2)
        {
            var second = spy.ReceivedWindows[1];
            var dc2 = new float[5];
            for (int n = 0; n < 5; n++)
                dc2[n] = (float)second[n * 481].Real;
            for (int n = 1; n < 5; n++)
                Assert.True(dc2[n] > dc2[n - 1]);
            Assert.True(dc2[0] > dc[0], "Queue did not advance between emissions.");
        }
    }
}
