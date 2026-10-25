namespace Denoiser.Core;

/// <summary>
/// Guarantees that a consumer only ever receives exact <see cref="FrameSize"/>-sample frames.
/// Leftover samples from a push are retained until the next push or <see cref="Flush"/>.
/// <see cref="Flush"/> zero-pads the final partial frame (documented behaviour).
/// </summary>
public sealed class FrameAccumulator
{
    private readonly int _frameSize;
    private readonly float[] _buf;
    private int _fill;

    public FrameAccumulator(int frameSize)
    {
        if (frameSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameSize));
        _frameSize = frameSize;
        _buf = new float[frameSize];
    }

    public int FrameSize => _frameSize;

    /// <summary>Number of samples currently retained (0 .. FrameSize-1).</summary>
    public int Pending => _fill;

    /// <summary>
    /// Callback receiving a full frame. The buffer is only valid for the duration of the call —
    /// copy or write to the sink inside the callback before returning.
    /// </summary>
    public delegate void FrameHandler(float[] frame, int length);

    /// <summary>
    /// Feeds input, invoking <paramref name="onFrame"/> exactly once per complete frame.
    /// Any leftover is retained for the next call.
    /// </summary>
    public void Push(ReadOnlySpan<float> input, FrameHandler onFrame)
    {
        int off = 0;
        while (off < input.Length)
        {
            int need = _frameSize - _fill;
            int take = Math.Min(need, input.Length - off);
            input.Slice(off, take).CopyTo(_buf.AsSpan(_fill));
            _fill += take;
            off += take;

            if (_fill == _frameSize)
            {
                onFrame(_buf, _frameSize);
                _fill = 0;
            }
        }
    }

    /// <summary>
    /// Zero-pads and emits a final partial frame. Call once at end-of-stream.
    /// Returns true if a frame was emitted.
    /// </summary>
    public bool Flush(FrameHandler onFrame)
    {
        if (_fill == 0)
            return false;

        Array.Clear(_buf, _fill, _frameSize - _fill);
        onFrame(_buf, _frameSize);
        _fill = 0;
        return true;
    }

    public void Reset()
    {
        _fill = 0;
        Array.Clear(_buf);
    }
}
