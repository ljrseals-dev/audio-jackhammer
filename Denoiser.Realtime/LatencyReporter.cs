namespace Denoiser.Realtime;

public sealed record LatencyBudget(
    int CaptureBufferMs,
    int AlgorithmicMs,
    int RenderBufferMs)
{
    public int TotalMs => CaptureBufferMs + AlgorithmicMs + RenderBufferMs;

    public string Describe() =>
        $"capture={CaptureBufferMs}ms + algorithmic={AlgorithmicMs}ms + render={RenderBufferMs}ms = {TotalMs}ms";
}

public sealed class LatencyReporter
{
    private readonly LatencyBudget _budget;
    private long _underruns;
    private long _overruns;

    public LatencyReporter(LatencyBudget budget) => _budget = budget;

    public void RecordUnderrun() => Interlocked.Increment(ref _underruns);
    public void RecordOverrun() => Interlocked.Increment(ref _overruns);

    public long Underruns => Interlocked.Read(ref _underruns);
    public long Overruns => Interlocked.Read(ref _overruns);

    public string Snapshot() =>
        $"budget: {_budget.Describe()}; underruns={_underruns}; overruns={_overruns}";
}
