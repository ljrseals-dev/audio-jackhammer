using Denoiser.Realtime;
using Xunit;

namespace Denoiser.Tests;

public sealed class LatencyReporterTests
{
    [Fact]
    public void Budget_TotalMs_IsSumOfComponents()
    {
        var budget = new LatencyBudget(10, 40, 10);
        Assert.Equal(60, budget.TotalMs);
        Assert.Contains("60ms", budget.Describe());
    }

    [Fact]
    public void UnderrunAndOverrun_AreCounted()
    {
        var r = new LatencyReporter(new LatencyBudget(10, 40, 10));
        r.RecordUnderrun();
        r.RecordUnderrun();
        r.RecordOverrun();
        Assert.Equal(2, r.Underruns);
        Assert.Equal(1, r.Overruns);
        Assert.Contains("underruns=2", r.Snapshot());
    }
}
