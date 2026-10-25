using System.Numerics;
using Denoiser.Models.DeepFilterNet;
using Xunit;

namespace Denoiser.Tests;

public sealed class DeepFilterKernelTests
{
    private const int Bins = 481;
    private const int DfBins = 96;
    private const int Order = 5;

    private static Complex[] DistinctWindow()
    {
        var w = new Complex[Order * Bins];
        for (int n = 0; n < Order; n++)
            for (int k = 0; k < Bins; k++)
                w[n * Bins + k] = new Complex(n + 1, 0);
        return w;
    }

    [Fact]
    public void Tap2_IsIdentity_OnCurrentFrame()
    {
        var w = DistinctWindow();
        var coefs = new float[Order * DfBins * 2];
        for (int k = 0; k < DfBins; k++)
            coefs[(2 * DfBins + k) * 2] = 1f;

        var masked = new Complex[Bins];
        DeepFilterKernel.Apply(w, coefs, masked);

        for (int k = 0; k < DfBins; k++)
            Assert.Equal(new Complex(3, 0), masked[k]);
    }

    [Fact]
    public void Tap0_UsesFrame_tMinus2_NotCurrentFrame()
    {
        var w = DistinctWindow();
        var coefs = new float[Order * DfBins * 2];
        for (int k = 0; k < DfBins; k++)
            coefs[(0 * DfBins + k) * 2] = 1f;

        var masked = new Complex[Bins];
        DeepFilterKernel.Apply(w, coefs, masked);

        for (int k = 0; k < DfBins; k++)
            Assert.Equal(new Complex(1, 0), masked[k]);
    }

    [Fact]
    public void Tap4_UsesFrame_tPlus2()
    {
        var w = DistinctWindow();
        var coefs = new float[Order * DfBins * 2];
        for (int k = 0; k < DfBins; k++)
            coefs[(4 * DfBins + k) * 2] = 1f;

        var masked = new Complex[Bins];
        DeepFilterKernel.Apply(w, coefs, masked);

        for (int k = 0; k < DfBins; k++)
            Assert.Equal(new Complex(5, 0), masked[k]);
    }

    [Fact]
    public void BinsAbove96_AreUntouched()
    {
        var w = DistinctWindow();
        var coefs = new float[Order * DfBins * 2];
        var masked = new Complex[Bins];
        for (int k = DfBins; k < Bins; k++)
            masked[k] = new Complex(42, -42);

        DeepFilterKernel.Apply(w, coefs, masked);

        for (int k = DfBins; k < Bins; k++)
            Assert.Equal(new Complex(42, -42), masked[k]);
    }
}
