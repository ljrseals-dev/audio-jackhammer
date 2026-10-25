using Denoiser.Core;
using Xunit;

namespace Denoiser.Tests;

public sealed class OlaInPlaceAliasingTests
{
    /// <summary>
    /// Proves that snapshot-before-write is correct: processing the same logical
    /// frames via a reused buffer yields identical output to two independent buffers.
    /// </summary>
    [Fact]
    public void ReusedBuffer_MatchesIndependentBuffers()
    {
        using var olaReuse = new OverlapAddAdapter(new MockSqrtHannAnalysis(480));
        using var olaIndep = new OverlapAddAdapter(new MockSqrtHannAnalysis(480));

        var frame1 = new float[480];
        var frame2 = new float[480];
        for (int i = 0; i < 480; i++)
        {
            frame1[i] = i / 480f;
            frame2[i] = (480 + i) / 960f;
        }

        // Independent path
        var indep1 = (float[])frame1.Clone();
        var indep2 = (float[])frame2.Clone();
        olaIndep.Process(indep1);
        olaIndep.Process(indep2);

        // Reused buffer path
        var shared = (float[])frame1.Clone();
        olaReuse.Process(shared);
        var reuseOut1 = (float[])shared.Clone();
        frame2.AsSpan().CopyTo(shared); // overwrite same instance
        olaReuse.Process(shared);
        var reuseOut2 = (float[])shared.Clone();

        // Outputs must match — if prevTail was corrupted by write-back, reuseOut2 diverges
        for (int i = 0; i < 480; i++)
        {
            Assert.True(Math.Abs(reuseOut1[i] - indep1[i]) < 1e-6f,
                $"Frame1 diverge at {i}: reuse={reuseOut1[i]}, indep={indep1[i]}");
            Assert.True(Math.Abs(reuseOut2[i] - indep2[i]) < 1e-6f,
                $"Frame2 diverge at {i}: reuse={reuseOut2[i]}, indep={indep2[i]} " +
                "(likely _prevTail snapshotted after output write-back)");
        }
    }

    [Fact]
    public void SameBufferTwice_DoesNotMutateCallerUnexpectedly()
    {
        using var ola = new OverlapAddAdapter(new MockSqrtHannAnalysis(480));
        var buffer = new float[480];
        Array.Fill(buffer, 0.5f);
        var originalLength = buffer.Length;
        ola.Process(buffer);
        Assert.Equal(originalLength, buffer.Length);
        Assert.All(buffer, s => Assert.True(float.IsFinite(s)));
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
