using Denoiser.Core;
using Xunit;

namespace Denoiser.Tests;

public sealed class OverlapAddAdapterTests
{
    /// <summary>
    /// Mock STFT model: applies sqrt-Hann analysis internally, then identity.
    /// Matches the assumption the adapter makes about its inner model.
    /// </summary>
    private sealed class MockStftIdentityDenoiser : IDenoiser
    {
        private readonly float[] _analysisWindow;

        public MockStftIdentityDenoiser(int frameSize)
        {
            FrameSize = frameSize;
            _analysisWindow = new float[frameSize];
            for (int i = 0; i < frameSize; i++)
            {
                double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / frameSize));
                _analysisWindow[i] = (float)Math.Sqrt(hann);
            }
        }

        public int FrameSize { get; }
        public int SampleRate => 48000;
        public string Name => "mock-stft";
        public bool RequiresOverlapAdd => false;

        public void Process(Span<float> frame)
        {
            for (int i = 0; i < frame.Length; i++)
                frame[i] *= _analysisWindow[i];
        }

        public void Dispose() { }
    }

    private const int FrameSize = 480;

    [Fact]
    public void ConstantSignal_ReconstructsConstant()
    {
        var inner = new MockStftIdentityDenoiser(FrameSize);
        using var ola = new OverlapAddAdapter(inner);

        var input = new float[FrameSize * 20];
        Array.Fill(input, 1f);

        var output = new List<float>();
        for (int i = 0; i + FrameSize <= input.Length; i += FrameSize)
        {
            var frame = input.AsSpan(i, FrameSize).ToArray();
            ola.Process(frame);
            output.AddRange(frame);
        }

        // Skip the first FrameSize samples: warm-up includes zero-padded prevTail.
        for (int i = FrameSize; i < output.Count; i++)
        {
            Assert.InRange(output[i], 0.99f, 1.01f);
        }
    }

    [Fact]
    public void SineWave_NoDiscontinuities()
    {
        var inner = new MockStftIdentityDenoiser(FrameSize);
        using var ola = new OverlapAddAdapter(inner);

        const int sampleRate = 48000;
        const float freq = 440f;
        var input = new float[FrameSize * 30];
        for (int i = 0; i < input.Length; i++)
            input[i] = (float)Math.Sin(2 * Math.PI * freq * i / sampleRate);

        var output = new List<float>();
        for (int i = 0; i + FrameSize <= input.Length; i += FrameSize)
        {
            var frame = input.AsSpan(i, FrameSize).ToArray();
            ola.Process(frame);
            output.AddRange(frame);
        }

        for (int i = FrameSize + 1; i < output.Count; i++)
        {
            float delta = Math.Abs(output[i] - output[i - 1]);
            Assert.True(delta < 0.1f,
                $"Discontinuity at output[{i}]: delta={delta:F4}");
        }
    }

    [Fact]
    public void Process_RejectsWrongFrameSize()
    {
        using var ola = new OverlapAddAdapter(new MockStftIdentityDenoiser(FrameSize));
        Assert.Throws<ArgumentException>(() => ola.Process(new float[FrameSize - 1]));
        Assert.Throws<ArgumentException>(() => ola.Process(new float[FrameSize + 1]));
    }

    [Fact]
    public void Process_AfterFlush_StartsNewStream()
    {
        using var ola = new OverlapAddAdapter(new MockStftIdentityDenoiser(FrameSize));
        ola.Process(new float[FrameSize]);
        var dest = new float[FrameSize];
        ola.Flush(dest);
        // Restart is allowed after Flush (new stream).
        ola.Process(new float[FrameSize]);
    }

    [Fact]
    public void WrapsAlreadyOlaDenoiser_Throws()
    {
        var inner = new MockRequiresOla();
        Assert.Throws<ArgumentException>(() => new OverlapAddAdapter(inner));
    }

    [Fact]
    public void OlaOff_NullDenoiser_IsBitIdentical()
    {
        var denoiser = new NullDenoiser();
        var original = new float[480];
        for (int i = 0; i < original.Length; i++)
            original[i] = (float)Math.Sin(2 * Math.PI * 440 * i / 48000.0);
        var copy = (float[])original.Clone();
        denoiser.Process(copy);
        Assert.Equal(original, copy);
    }

    private sealed class MockRequiresOla : IDenoiser
    {
        public int FrameSize => 480;
        public int SampleRate => 48000;
        public string Name => "mock-requires-ola";
        public bool RequiresOverlapAdd => true;
        public void Process(Span<float> frame) { }
        public void Dispose() { }
    }
}
