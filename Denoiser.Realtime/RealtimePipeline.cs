using Denoiser.Core;

namespace Denoiser.Realtime;

/// <summary>
/// Three-thread realtime path: capture → SPSC → DSP → SPSC → render.
/// No allocation / locks / exceptions on capture or render callbacks.
/// </summary>
public sealed class RealtimePipeline : IDisposable
{
    private readonly IAudioCaptureDevice _capture;
    private readonly IAudioRenderDevice _render;
    private readonly IDenoiser _denoiser;
    private readonly SoftLimiter? _limiter;
    private readonly SpscRingBuffer _captureRing;
    private readonly SpscRingBuffer _renderRing;
    private readonly LatencyReporter _latency;
    private readonly CancellationTokenSource _cts = new();
    private readonly int _frameSize;
    private Thread? _dspThread;
    private bool _started;

    public RealtimePipeline(
        IAudioCaptureDevice capture,
        IAudioRenderDevice render,
        IDenoiser denoiser,
        LatencyBudget budget,
        SoftLimiter? limiter = null,
        int ringSeconds = 2)
    {
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _render = render ?? throw new ArgumentNullException(nameof(render));
        _denoiser = denoiser ?? throw new ArgumentNullException(nameof(denoiser));
        _limiter = limiter;
        _frameSize = denoiser.FrameSize;
        _captureRing = new SpscRingBuffer(NextPow2(48000 * Math.Max(1, ringSeconds)));
        _renderRing = new SpscRingBuffer(NextPow2(48000 * Math.Max(1, ringSeconds)));
        _latency = new LatencyReporter(budget);
    }

    public LatencyReporter Latency => _latency;

    public void Start()
    {
        if (_started) return;
        _started = true;
        Console.WriteLine($"[realtime] model={_denoiser.Name} framesize={_frameSize}");
        Console.WriteLine($"[realtime] {_latency.Snapshot()}");

        _capture.DataAvailable += OnCapture;
        _render.RenderRequested += OnRender;

        _dspThread = new Thread(DspLoop)
        {
            Name = "Denoiser.DSP",
            Priority = ThreadPriority.AboveNormal,
            IsBackground = true,
        };
        _dspThread.Start();

        _capture.Start();
        _render.Start();
    }

    private void OnCapture(ReadOnlySpan<float> samples)
    {
        try
        {
            int written = _captureRing.Write(samples);
            if (written < samples.Length)
                _latency.RecordOverrun();
        }
        catch
        {
            // Never throw out of capture callback.
        }
    }

    private void OnRender(Span<float> buffer)
    {
        try
        {
            int read = _renderRing.Read(buffer);
            if (read < buffer.Length)
            {
                buffer.Slice(read).Clear();
                _latency.RecordUnderrun();
            }
        }
        catch
        {
            buffer.Clear();
        }
    }

    private void DspLoop()
    {
        var token = _cts.Token;
        var scratch = new float[Math.Max(_frameSize, 512)];
        var accumulator = new FrameAccumulator(_frameSize);

        while (!token.IsCancellationRequested)
        {
            int n = _captureRing.Read(scratch);
            if (n == 0)
            {
                Thread.Sleep(1);
                continue;
            }

            try
            {
                accumulator.Push(scratch.AsSpan(0, n), OnFullFrame);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[realtime] DSP error: {ex.Message}");
            }
        }

        try
        {
            accumulator.Flush(OnFullFrame);
            if (_denoiser is IFlushableDenoiser flushable)
            {
                var tail = new float[_frameSize * 8];
                int w = flushable.Flush(tail);
                if (w > 0)
                {
                    var span = tail.AsSpan(0, w);
                    _limiter?.Process(span);
                    _renderRing.Write(span);
                }
            }
        }
        catch { /* shutdown */ }
    }

    private void OnFullFrame(float[] frame, int length)
    {
        var span = frame.AsSpan(0, length);
        _denoiser.Process(span);
        _limiter?.Process(span);
        int written = _renderRing.Write(span);
        if (written < length)
            _latency.RecordOverrun();
    }

    public void Stop()
    {
        if (!_started) return;
        _cts.Cancel();
        try { _capture.Stop(); } catch { }
        try { _render.Stop(); } catch { }
        _dspThread?.Join(TimeSpan.FromSeconds(5));
        Console.WriteLine($"[realtime] final: {_latency.Snapshot()}");
        _started = false;
    }

    public void Dispose()
    {
        Stop();
        _capture.Dispose();
        _render.Dispose();
        _denoiser.Dispose();
        _cts.Dispose();
    }

    private static int NextPow2(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}
