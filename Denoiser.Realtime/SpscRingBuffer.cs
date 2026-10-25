using System.Threading;

namespace Denoiser.Realtime;

/// <summary>
/// Single-producer / single-consumer lock-free ring buffer of float.
/// Capacity must be a power of two. Wait-free under the SPSC contract.
/// </summary>
public sealed class SpscRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private long _writePos;
    private long _readPos;

    public SpscRingBuffer(int capacity)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
            throw new ArgumentException("Capacity must be a power of two.", nameof(capacity));
        _buffer = new float[capacity];
        _mask = capacity - 1;
    }

    public int Capacity => _buffer.Length;

    public int AvailableToRead
    {
        get
        {
            long w = Volatile.Read(ref _writePos);
            long r = Volatile.Read(ref _readPos);
            return (int)(w - r);
        }
    }

    public int AvailableToWrite => _buffer.Length - AvailableToRead;

    public int Write(ReadOnlySpan<float> source)
    {
        long w = _writePos;
        long r = Volatile.Read(ref _readPos);
        int free = _buffer.Length - (int)(w - r);
        int n = Math.Min(free, source.Length);
        for (int i = 0; i < n; i++)
            _buffer[(int)((w + i) & _mask)] = source[i];
        Volatile.Write(ref _writePos, w + n);
        return n;
    }

    public int Read(Span<float> destination)
    {
        long r = _readPos;
        long w = Volatile.Read(ref _writePos);
        int available = (int)(w - r);
        int n = Math.Min(available, destination.Length);
        for (int i = 0; i < n; i++)
            destination[i] = _buffer[(int)((r + i) & _mask)];
        Volatile.Write(ref _readPos, r + n);
        return n;
    }

    public int Skip(int n)
    {
        long r = _readPos;
        long w = Volatile.Read(ref _writePos);
        int available = (int)(w - r);
        int skip = Math.Min(available, n);
        Volatile.Write(ref _readPos, r + skip);
        return skip;
    }

    public void Clear()
    {
        Volatile.Write(ref _readPos, Volatile.Read(ref _writePos));
    }
}
