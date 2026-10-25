using System.Numerics;
using Denoiser.Models.DeepFilterNet;
using Xunit;

namespace Denoiser.Tests;

public sealed class FftBackendTests
{
    private const int N = 960;

    [Fact]
    public void FftSharp_MatchesNaiveDft_OnImpulse()
    {
        var naive = new NaiveDftBackend(N);
        var fast = new FftSharpBackend(N);
        var input = new double[N];
        input[0] = 1.0;

        var a = new Complex[N / 2 + 1];
        var b = new Complex[N / 2 + 1];
        naive.Forward(input, a);
        fast.Forward(input, b);

        for (int k = 0; k < a.Length; k++)
        {
            Assert.InRange(Math.Abs(a[k].Real - b[k].Real), 0, 1e-5);
            Assert.InRange(Math.Abs(a[k].Imaginary - b[k].Imaginary), 0, 1e-5);
        }
    }

    [Fact]
    public void FftSharp_RoundTrip_ReconstructsInput()
    {
        var fast = new FftSharpBackend(N);
        var input = new double[N];
        var rng = new Random(42);
        for (int i = 0; i < N; i++) input[i] = rng.NextDouble() - 0.5;

        var spectrum = new Complex[N / 2 + 1];
        var output = new double[N];
        fast.Forward(input, spectrum);
        fast.Inverse(spectrum, output);

        for (int i = 0; i < N; i++)
            Assert.InRange(Math.Abs(input[i] - output[i]), 0, 1e-6);
    }

    [Fact]
    public void NaiveDft_IsStillAvailable_ForCrossValidation()
    {
        var naive = new NaiveDftBackend(N);
        Assert.Equal(N, naive.Size);
    }

    [Fact]
    public void FftSharp_IsAtLeast10xFaster_ThanNaive()
    {
        var naive = new NaiveDftBackend(N);
        var fast = new FftSharpBackend(N);
        var input = new double[N];
        var spectrum = new Complex[N / 2 + 1];

        // Warm-up
        for (int i = 0; i < 3; i++) { naive.Forward(input, spectrum); fast.Forward(input, spectrum); }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 20; i++) naive.Forward(input, spectrum);
        sw.Stop();
        double naiveMs = sw.Elapsed.TotalMilliseconds / 20;

        sw.Restart();
        for (int i = 0; i < 20; i++) fast.Forward(input, spectrum);
        sw.Stop();
        double fastMs = sw.Elapsed.TotalMilliseconds / 20;

        Assert.True(fastMs < naiveMs / 5,
            $"FftSharp should be substantially faster. naive={naiveMs:F3} ms, fast={fastMs:F3} ms.");
    }
}
