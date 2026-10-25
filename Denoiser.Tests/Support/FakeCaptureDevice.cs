using Denoiser.Realtime;

namespace Denoiser.Tests.Support;

internal sealed class FakeCaptureDevice : IAudioCaptureDevice
{
    private readonly float[] _source;
    private Thread? _thread;
    private volatile bool _running;

    public FakeCaptureDevice(float[] source, int sampleRate = 48000)
    {
        _source = source;
        SampleRate = sampleRate;
    }

    public int SampleRate { get; }
    public int Channels => 1;
    public event CaptureDataHandler? DataAvailable;

    public void Start()
    {
        _running = true;
        _thread = new Thread(() =>
        {
            int pos = 0;
            while (_running && pos < _source.Length)
            {
                int n = Math.Min(480, _source.Length - pos);
                DataAvailable?.Invoke(_source.AsSpan(pos, n));
                pos += n;
                Thread.Sleep(1);
            }
        }) { IsBackground = true };
        _thread.Start();
    }

    public void Stop() => _running = false;
    public void Dispose() => Stop();
}

internal sealed class FakeRenderDevice : IAudioRenderDevice
{
    private readonly List<float> _captured = new();
    private Thread? _thread;
    private volatile bool _running;

    public FakeRenderDevice(int sampleRate = 48000) => SampleRate = sampleRate;

    public int SampleRate { get; }
    public int Channels => 1;
    public IReadOnlyList<float> Captured
    {
        get { lock (_captured) return _captured.ToArray(); }
    }
    public event RenderRequestHandler? RenderRequested;

    public void Start()
    {
        _running = true;
        _thread = new Thread(() =>
        {
            var buffer = new float[480];
            while (_running)
            {
                RenderRequested?.Invoke(buffer);
                lock (_captured) _captured.AddRange(buffer);
                Thread.Sleep(1);
            }
        }) { IsBackground = true };
        _thread.Start();
    }

    public void Stop() => _running = false;
    public void Dispose() => Stop();
}
