using Denoiser.Models.DeepFilterNet;
using Xunit;

namespace Denoiser.Tests;

public sealed class PadFeatTests
{
    [Fact]
    public void Batch_DropsLookaheadFrames_AndZeroFillsTail()
    {
        var input = new float[] { 0, 1, 2, 3, 4, 5 };
        var output = new float[6];
        PadFeat.ApplyBatch(input, output, t: 6, f: 1);
        Assert.Equal(new float[] { 2, 3, 4, 5, 0, 0 }, output);
    }

    [Fact]
    public void Batch_IsNoOpDependency_OnLookaheadConstant()
    {
        Assert.Equal(2, PadFeat.ConvLookahead);
    }

    [Fact]
    public void Streamer_DelaysByLookahead()
    {
        var s = new PadFeat.Streamer(featureLength: 1);
        var encoderInput = new float[1];

        Assert.False(s.TryPush(new float[] { 0 }, encoderInput));
        Assert.False(s.TryPush(new float[] { 1 }, encoderInput));
        Assert.True(s.TryPush(new float[] { 2 }, encoderInput));
        Assert.Equal(0f, encoderInput[0]);

        Assert.True(s.TryPush(new float[] { 3 }, encoderInput));
        Assert.Equal(1f, encoderInput[0]);
    }
}
