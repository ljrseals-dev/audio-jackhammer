namespace Denoiser.Core;

/// <summary>
/// Wraps a frame-based STFT denoiser with 50%-overlap weighted overlap-add.
///
/// Assumptions:
///   - The wrapped denoiser applies a matching analysis window internally
///     (sqrt-Hann for default OlaWindowMode.SqrtHann).
///   - The wrapped denoiser's RequiresOverlapAdd is false (adapter terminates the chain).
///
/// I/O contract: exactly FrameSize samples in, exactly FrameSize samples out (in-place).
/// First call includes warm-up (prevTail starts zero).
/// Call Flush at end-of-stream to drain the final hop; returns valid sample count.
/// </summary>
public sealed class OverlapAddAdapter : IFlushableDenoiser
{
    private readonly IDenoiser _inner;
    private readonly int _frameSize;
    private readonly int _hopSize;

    private readonly float[] _synthesisWindow;
    private readonly float[] _accumulator;
    private readonly float[] _prevTail;
    private readonly float[] _half2Snapshot; // explicit snapshot before any write
    private readonly float[] _frameA;
    private readonly float[] _frameB;
    private readonly float[] _work;
    private readonly float[] _emitScratch;

    private bool _flushed;
    private bool _hasProcessed;
    private bool _disposed;

    public OverlapAddAdapter(IDenoiser inner, OlaWindowMode windowMode = OlaWindowMode.SqrtHann)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        if (inner.FrameSize <= 0 || (inner.FrameSize & 1) != 0)
            throw new ArgumentException(
                "FrameSize must be a positive even integer for 50% overlap.",
                nameof(inner));

        if (inner.RequiresOverlapAdd)
            throw new ArgumentException(
                "Inner denoiser already requires OLA; do not double-wrap.",
                nameof(inner));

        _frameSize = inner.FrameSize;
        _hopSize = _frameSize / 2;

        _synthesisWindow = BuildWindow(_frameSize, windowMode);
        _accumulator = new float[_frameSize];
        _prevTail = new float[_hopSize];
        _half2Snapshot = new float[_hopSize];
        _frameA = new float[_frameSize];
        _frameB = new float[_frameSize];
        _work = new float[_frameSize];
        _emitScratch = new float[_frameSize];
    }

    public int FrameSize => _frameSize;
    public int SampleRate => _inner.SampleRate;
    public string Name => _inner.Name + "+ola";
    public bool RequiresOverlapAdd => false;

    public void Process(Span<float> frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (frame.Length != _frameSize)
            throw new ArgumentException(
                $"Expected {_frameSize} samples, got {frame.Length}.", nameof(frame));

        if (_flushed)
        {
            _flushed = false;
            Array.Clear(_prevTail);
            Array.Clear(_accumulator);
            _hasProcessed = false;
        }

        // Snapshot BOTH halves from the caller's buffer BEFORE any mutation.
        var half1 = frame.Slice(0, _hopSize);
        var half2 = frame.Slice(_hopSize, _hopSize);
        half2.CopyTo(_half2Snapshot); // critical: before output write-back

        int emitPos = 0;

        // Frame A = [prevTail, half1]
        _prevTail.AsSpan().CopyTo(_frameA.AsSpan(0, _hopSize));
        half1.CopyTo(_frameA.AsSpan(_hopSize));
        emitPos = AccumulateAndEmit(_frameA, emitPos);

        // Frame B = [half1, half2]
        half1.CopyTo(_frameB.AsSpan(0, _hopSize));
        half2.CopyTo(_frameB.AsSpan(_hopSize));
        emitPos = AccumulateAndEmit(_frameB, emitPos);

        // Commit tail from the pre-write snapshot
        _half2Snapshot.AsSpan().CopyTo(_prevTail);

        // Write output back into the caller's buffer
        _emitScratch.AsSpan(0, _frameSize).CopyTo(frame);
        _hasProcessed = true;
    }

    public int Flush(Span<float> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_flushed)
            return 0;
        _flushed = true;

        if (!_hasProcessed)
            return 0;

        if (destination.Length < _hopSize)
            throw new ArgumentException($"Destination must be at least {_hopSize} samples.");

        int emitPos = 0;

        // Final frame: [prevTail, zeros]
        _prevTail.AsSpan().CopyTo(_frameA.AsSpan(0, _hopSize));
        Array.Clear(_frameA, _hopSize, _hopSize);
        emitPos = AccumulateAndEmit(_frameA, emitPos);

        // Drain remaining half of the accumulator
        Array.Copy(_accumulator, 0, _emitScratch, emitPos, _hopSize);
        emitPos += _hopSize;
        Array.Clear(_accumulator, 0, _accumulator.Length);

        int n = Math.Min(emitPos, destination.Length);
        // Prefer returning only the fully-drained hop if destination is large;
        // after a complete Process sequence the pending tail is one hop.
        // emitPos is 2*hop after final AccumulateAndEmit + drain.
        // First hop is fully resolved; second is the drained remainder.
        _emitScratch.AsSpan(0, n).CopyTo(destination.Slice(0, n));
        return n;
    }

    private int AccumulateAndEmit(float[] frameBuf, int emitPos)
    {
        frameBuf.AsSpan().CopyTo(_work);
        _inner.Process(_work);

        for (int i = 0; i < _frameSize; i++)
            _accumulator[i] += _work[i] * _synthesisWindow[i];

        Array.Copy(_accumulator, 0, _emitScratch, emitPos, _hopSize);
        emitPos += _hopSize;

        Array.Copy(_accumulator, _hopSize, _accumulator, 0, _hopSize);
        Array.Clear(_accumulator, _hopSize, _hopSize);

        return emitPos;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inner.Dispose();
    }

    private static float[] BuildWindow(int n, OlaWindowMode mode)
    {
        var w = new float[n];
        for (int i = 0; i < n; i++)
        {
            double hann = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / n));
            w[i] = mode switch
            {
                OlaWindowMode.SqrtHann => (float)Math.Sqrt(hann),
                OlaWindowMode.Hann => (float)hann,
                OlaWindowMode.Rectangular => 1f,
                OlaWindowMode.Vorbis => (float)Math.Sin(Math.PI / 2.0 * Math.Pow(Math.Sin(Math.PI * (i + 0.5) / n), 2)),
                _ => (float)Math.Sqrt(hann),
            };
        }
        return w;
    }
}
