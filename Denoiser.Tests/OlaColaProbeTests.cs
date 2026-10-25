using Denoiser.Core;
using Denoiser.Tests.Support;
using Denoiser.Models;
using Xunit;

namespace Denoiser.Tests;

public sealed class OlaColaProbeTests
{
    [Theory]
    [InlineData(1.0f)]
    [InlineData(0.5f)]
    [InlineData(-0.7f)]
    public void RnNoise_SatisfiesCola_OnConstantInput(float amplitude)
    {
        IDenoiser model;
        try { model = new RnNoiseDenoiser(); }
        catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException)
        {
            Skip.Unless(false, $"RNNoise native library unavailable: {ex.Message}");
            return;
        }

        using (model)
        {
            // RNNoise does not require OLA. Skip wrap; the point is to NOT wrap it.
            if (!model.RequiresOverlapAdd)
            {
                Console.WriteLine("[INFO] RNNoise.RequiresOverlapAdd=false — OLA not applied (correct).");
                return;
            }

            using var ola = new OverlapAddAdapter(model);
            var output = RunConstant(ola, amplitude, frames: 20);
            for (int i = ola.FrameSize; i < output.Count; i++)
                Assert.InRange(output[i], amplitude * 0.99f, amplitude * 1.01f);
        }
    }


    [Fact]
    public void MockSqrtHann_SatisfiesCola_WithSqrtHannSynthesis()
    {
        using var ola = new OverlapAddAdapter(new MockSqrtHann(480), OlaWindowMode.SqrtHann);
        var output = RunConstant(ola, 1.0f, frames: 20);
        for (int i = ola.FrameSize; i < output.Count; i++)
            Assert.InRange(output[i], 0.99f, 1.01f);
    }

    private static List<float> RunConstant(IDenoiser ola, float amplitude, int frames)
    {
        var buf = new float[ola.FrameSize];
        Array.Fill(buf, amplitude);
        var output = new List<float>(ola.FrameSize * frames);
        for (int i = 0; i < frames; i++)
        {
            ola.Process(buf);
            output.AddRange(buf);
            Array.Fill(buf, amplitude);
        }
        return output;
    }

    private sealed class MockSqrtHann : IDenoiser
    {
        private readonly float[] _w;
        public MockSqrtHann(int n)
        {
            FrameSize = n;
            _w = new float[n];
            for (int i = 0; i < n; i++)
            {
                double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / n));
                _w[i] = (float)Math.Sqrt(hann);
            }
        }
        public int FrameSize { get; }
        public int SampleRate => 48000;
        public string Name => "mock-sqrthann";
        public bool RequiresOverlapAdd => false;
        public void Process(Span<float> frame)
        {
            for (int i = 0; i < frame.Length; i++) frame[i] *= _w[i];
        }
        public void Dispose() { }
    }
}
