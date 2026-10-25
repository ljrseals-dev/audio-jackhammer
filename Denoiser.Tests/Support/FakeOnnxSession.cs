using Denoiser.Models.DeepFilterNet;

namespace Denoiser.Tests.Support;

internal sealed class FakeOnnxSession : IDeepFilterNetOnnxSession
{
    public const int Order = 5;
    public const int DfBins = 96;
    public const int ErbBands = 32;

    public int RunCount { get; private set; }

    public (float[] erbMask, float[] dfCoefs) Run(float[] featErb, float[] featSpec, int t)
    {
        RunCount++;
        var mask = new float[t * ErbBands];
        Array.Fill(mask, 1f);
        var coefs = new float[Order * t * DfBins * 2];
        for (int i = 0; i < t; i++)
            for (int k = 0; k < DfBins; k++)
                coefs[(((2 * t) + i) * DfBins + k) * 2] = 1f;
        return (mask, coefs);
    }

    public void Dispose() { }
}

internal sealed class RecordingOnnxSession : IDeepFilterNetOnnxSession
{
    public List<(float[] featErb, float[] featSpec, int t)> Calls { get; } = new();

    public (float[] erbMask, float[] dfCoefs) Run(float[] featErb, float[] featSpec, int t)
    {
        Calls.Add(((float[])featErb.Clone(), (float[])featSpec.Clone(), t));
        var mask = new float[t * 32];
        Array.Fill(mask, 1f);
        var coefs = new float[5 * t * 96 * 2];
        for (int i = 0; i < t; i++)
            for (int k = 0; k < 96; k++)
                coefs[(((2 * t) + i) * 96 + k) * 2] = 1f;
        return (mask, coefs);
    }

    public void Dispose() { }
}

internal sealed class SpyKernel : IDeepFilterKernel
{
    public List<System.Numerics.Complex[]> ReceivedWindows { get; } = new();
    private readonly IDeepFilterKernel _inner;
    public SpyKernel(IDeepFilterKernel inner) => _inner = inner;

    public void Apply(ReadOnlySpan<System.Numerics.Complex> noisyWindow, ReadOnlySpan<float> coefs, Span<System.Numerics.Complex> maskedSpec)
    {
        ReceivedWindows.Add(noisyWindow.ToArray());
        _inner.Apply(noisyWindow, coefs, maskedSpec);
    }
}

internal sealed class StubErbFilterbank : IErbFilterbank
{
    public void Forward(ReadOnlySpan<System.Numerics.Complex> spec, Span<float> erb) => erb.Clear();
    public void Inverse(ReadOnlySpan<float> erbGain, Span<float> binGain)
    {
        for (int i = 0; i < binGain.Length; i++) binGain[i] = 1f;
    }
}
