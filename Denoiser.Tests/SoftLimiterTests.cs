using Denoiser.Core;
using Xunit;

namespace Denoiser.Tests;

public sealed class SoftLimiterTests
{
    [Fact]
    public void NoLookAhead_LeavesSubCeilingUntouched()
    {
        var limiter = new SoftLimiter(threshold: 0.95f, knee: 0.05f, lookAhead: false);
        var buffer = new float[] { 0.1f, -0.2f, 0.5f, -0.8f };
        var expected = (float[])buffer.Clone();
        limiter.Process(buffer);
        Assert.Equal(expected, buffer);
    }

    [Fact]
    public void NoLookAhead_ClampsCeilingBreach()
    {
        var limiter = new SoftLimiter(threshold: 0.95f, knee: 0.05f, lookAhead: false);
        var buffer = new float[] { 2.0f, -2.0f };
        limiter.Process(buffer);
        Assert.All(buffer, s => Assert.InRange(MathF.Abs(s), 0f, 0.951f));
    }

    [Fact]
    public void LookAhead_PreventsOvershootOnStep()
    {
        var limiter = new SoftLimiter(
            threshold: 0.9f, knee: 0.02f,
            lookAhead: true, sampleRate: 48000,
            lookAheadMs: 5f, releaseMs: 100f);

        var buffer = new float[2000];
        for (int i = 0; i < 1000; i++) buffer[i] = 0.1f;
        for (int i = 1000; i < buffer.Length; i++) buffer[i] = 0.999f;

        limiter.Process(buffer);
        Assert.All(buffer, s => Assert.InRange(MathF.Abs(s), 0f, 0.91f));
    }

    [Fact]
    public void LookAhead_DelaysByRequestedAmount()
    {
        var limiter = new SoftLimiter(
            threshold: 0.9f, knee: 0.02f,
            lookAhead: true, sampleRate: 48000,
            lookAheadMs: 5f, releaseMs: 100f);

        int delay = limiter.DelaySamples; // pow2(240) = 256
        var buffer = new float[delay + 10];
        buffer[0] = 0.5f;
        limiter.Process(buffer);

        Assert.Equal(0f, buffer[0]);
        Assert.InRange(buffer[delay], 0.49f, 0.5f);
    }

    [Fact]
    public void LookAhead_RejectsBadReleaseRatio()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SoftLimiter(lookAhead: true, lookAheadMs: 10f, releaseMs: 15f));
    }

    [Fact]
    public void LookAhead_HandlesArbitraryBufferLengths()
    {
        var limiter = new SoftLimiter(
            lookAhead: true, sampleRate: 48000,
            lookAheadMs: 5f, releaseMs: 100f);

        var chunks = new[] { 240, 240, 137, 240, 1, 480 };
        foreach (var len in chunks)
        {
            var b = new float[len];
            Array.Fill(b, 0.5f);
            limiter.Process(b);
            Assert.All(b, s => Assert.InRange(MathF.Abs(s), 0f, 0.951f));
        }
    }
}
