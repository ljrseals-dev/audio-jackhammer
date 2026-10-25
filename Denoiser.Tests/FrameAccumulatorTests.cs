using Denoiser.Core;
using Xunit;

namespace Denoiser.Tests;

public class FrameAccumulatorTests
{
    private const int FrameSize = 480;

    [Theory]
    [InlineData(1)]
    [InlineData(479)]
    [InlineData(480)]
    [InlineData(481)]
    [InlineData(4096)]
    [InlineData(4097)]
    public void PushAndFlush_EmitsExactCeilMultiple(int inputLength)
    {
        var framer = new FrameAccumulator(FrameSize);
        var emitted = new List<float[]>();

        var input = new float[inputLength];
        for (int i = 0; i < inputLength; i++)
            input[i] = i + 1;

        framer.Push(input, (frame, length) =>
        {
            emitted.Add(frame.AsSpan(0, length).ToArray());
        });

        framer.Flush((frame, length) =>
        {
            emitted.Add(frame.AsSpan(0, length).ToArray());
        });

        int expectedFrames = (inputLength + FrameSize - 1) / FrameSize;
        Assert.Equal(expectedFrames, emitted.Count);
        Assert.All(emitted, f => Assert.Equal(FrameSize, f.Length));
        Assert.Equal(expectedFrames * FrameSize, emitted.Sum(f => f.Length));
    }

    [Fact]
    public void Push_RetainsPartialAcrossCalls()
    {
        var framer = new FrameAccumulator(FrameSize);
        var emitted = new List<float[]>();

        var a = new float[200];
        for (int i = 0; i < 200; i++) a[i] = 10f + i;
        framer.Push(a, (frame, length) => emitted.Add(frame.AsSpan(0, length).ToArray()));
        Assert.Empty(emitted);
        Assert.Equal(200, framer.Pending);

        var b = new float[300];
        for (int i = 0; i < 300; i++) b[i] = 1000f + i;
        framer.Push(b, (frame, length) => emitted.Add(frame.AsSpan(0, length).ToArray()));
        Assert.Single(emitted);
        Assert.Equal(20, framer.Pending);

        var frame0 = emitted[0];
        for (int i = 0; i < 200; i++)
            Assert.Equal(10f + i, frame0[i]);
        for (int i = 0; i < 280; i++)
            Assert.Equal(1000f + i, frame0[200 + i]);
    }

    [Fact]
    public void Flush_ZeroPadsTail()
    {
        var framer = new FrameAccumulator(FrameSize);
        float[]? last = null;

        var input = new float[100];
        for (int i = 0; i < 100; i++) input[i] = 42f;
        framer.Push(input, (_, _) => { });

        bool emitted = framer.Flush((frame, length) => last = frame.AsSpan(0, length).ToArray());
        Assert.True(emitted);
        Assert.NotNull(last);
        Assert.Equal(FrameSize, last!.Length);

        for (int i = 0; i < 100; i++)
            Assert.Equal(42f, last[i]);
        for (int i = 100; i < FrameSize; i++)
            Assert.Equal(0f, last[i]);
    }

    [Fact]
    public void Flush_Empty_ReturnsFalse()
    {
        var framer = new FrameAccumulator(FrameSize);
        bool emitted = framer.Flush((_, _) => Assert.Fail("should not be called"));
        Assert.False(emitted);
    }

    [Fact]
    public void ExactMultiple_NoFlushNeeded()
    {
        var framer = new FrameAccumulator(FrameSize);
        int count = 0;
        var input = new float[FrameSize * 3];
        framer.Push(input, (_, _) => count++);
        Assert.Equal(3, count);
        Assert.Equal(0, framer.Pending);
        Assert.False(framer.Flush((_, _) => count++));
        Assert.Equal(3, count);
    }
}
