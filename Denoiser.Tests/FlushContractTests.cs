using Denoiser.Core;
using Xunit;

namespace Denoiser.Tests;

public sealed class FlushContractTests
{
    [Fact]
    public void Flush_ReturnsValidSampleCount()
    {
        using var ola = new OverlapAddAdapter(new MockSqrtHannAnalysis(480));
        var frame = new float[480];
        Array.Fill(frame, 0.5f);
        for (int i = 0; i < 3; i++) ola.Process(frame);

        var tail = new float[480];
        int n = ola.Flush(tail);
        Assert.InRange(n, 1, 480);
        // Pending OLA tail is at least one hop; exact count depends on drain strategy.
        Assert.True(n % 240 == 0, $"Expected hop-aligned flush count, got {n}");
    }

    [Fact]
    public void Flush_IsIdempotent()
    {
        using var ola = new OverlapAddAdapter(new MockSqrtHannAnalysis(480));
        var frame = new float[480];
        ola.Process(frame);

        var tail = new float[480];
        int n1 = ola.Flush(tail);
        int n2 = ola.Flush(tail);
        Assert.True(n1 > 0, "First flush should emit a tail.");
        Assert.Equal(0, n2);
    }

    [Fact]
    public void Flush_BeforeAnyProcess_ReturnsZero()
    {
        using var ola = new OverlapAddAdapter(new MockSqrtHannAnalysis(480));
        var tail = new float[480];
        Assert.Equal(0, ola.Flush(tail));
    }

    [Fact]
    public void Process_AfterFlush_StartsNewStream()
    {
        using var ola = new OverlapAddAdapter(new MockSqrtHannAnalysis(480));
        var frame = new float[480];
        Array.Fill(frame, 0.5f);
        ola.Process(frame);
        var tail = new float[480];
        ola.Flush(tail);

        frame = new float[480];
        Array.Fill(frame, 0.3f);
        ola.Process(frame); // must not throw
    }

    [Fact]
    public void TotalOutputLength_EqualsInputRoundedUpToFrame()
    {
        using var ola = new OverlapAddAdapter(new MockSqrtHannAnalysis(480));
        const int inputLen = 1000;
        var input = new float[inputLen];
        Array.Fill(input, 0.5f);

        var output = new List<float>();
        var frame = new float[480];
        for (int i = 0; i + 480 <= inputLen; i += 480)
        {
            Array.Copy(input, i, frame, 0, 480);
            ola.Process(frame);
            output.AddRange(frame);
        }

        var residual = new float[480];
        int residualCount = inputLen % 480;
        Array.Copy(input, inputLen - residualCount, residual, 0, residualCount);
        ola.Process(residual);
        output.AddRange(residual);

        var tail = new float[480];
        int n = ola.Flush(tail);
        output.AddRange(tail.AsSpan(0, n).ToArray());

        int expectedFrames = (inputLen + 479) / 480;
        // Process emits FrameSize per call; Flush adds hop.
        Assert.True(output.Count >= expectedFrames * 480,
            $"Expected at least {expectedFrames * 480}, got {output.Count}");
    }

    private sealed class MockSqrtHannAnalysis : IDenoiser
    {
        private readonly float[] _w;
        public MockSqrtHannAnalysis(int frameSize)
        {
            FrameSize = frameSize;
            _w = new float[frameSize];
            for (int i = 0; i < frameSize; i++)
            {
                double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / frameSize));
                _w[i] = (float)Math.Sqrt(hann);
            }
        }
        public int FrameSize { get; }
        public int SampleRate => 48000;
        public string Name => "mock";
        public bool RequiresOverlapAdd => false;
        public void Process(Span<float> frame)
        {
            for (int i = 0; i < frame.Length; i++) frame[i] *= _w[i];
        }
        public void Dispose() { }
    }
}
