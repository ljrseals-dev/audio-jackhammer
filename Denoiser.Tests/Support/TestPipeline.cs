using Denoiser.Core;

namespace Denoiser.Tests.Support;

/// <summary>
/// Lightweight helpers to run the pipeline over an in-memory float buffer
/// without going through file I/O.
/// </summary>
public static class TestPipeline
{
    public static float[] Run(
        float[] input,
        IDenoiser model,
        bool enableHighPass = true,
        float highPassHz = 90f,
        bool lookAhead = false,
        bool wrapOla = false,
        OlaWindowMode olaWindow = OlaWindowMode.SqrtHann)
    {
        IDenoiser effective = model;
        if (wrapOla)
            effective = new OverlapAddAdapter(model, olaWindow);

        var output = new List<float>(input.Length + effective.FrameSize * 2);
        using var source = new ArrayAudioSource(input, 48000);
        using var sink = new CollectingSink(output);
        var limiter = new SoftLimiter(lookAhead: lookAhead, releaseMs: 50f);
        using var pipeline = new DenoiserPipeline(
            source, effective, sink,
            highPassHz: highPassHz,
            enableHighPass: enableHighPass,
            limiter: limiter);
        pipeline.Run();
        return output.ToArray();
    }

    public sealed class ArrayAudioSource : IAudioSource
    {
        private readonly float[] _data;
        private int _pos;
        public ArrayAudioSource(float[] data, int sampleRate)
        {
            _data = data;
            SampleRate = sampleRate;
        }
        public int SampleRate { get; }
        public int Channels => 1;
        public TimeSpan? Duration => TimeSpan.FromSeconds(_data.Length / (double)SampleRate);
        public int Read(Span<float> buffer)
        {
            int n = Math.Min(buffer.Length, _data.Length - _pos);
            if (n <= 0) return 0;
            _data.AsSpan(_pos, n).CopyTo(buffer);
            _pos += n;
            return n;
        }
        public void Dispose() { }
    }

    public sealed class CollectingSink : IAudioSink
    {
        private readonly List<float> _target;
        public CollectingSink(List<float> target) => _target = target;
        public void Write(ReadOnlySpan<float> s)
        {
            for (int i = 0; i < s.Length; i++) _target.Add(s[i]);
        }
        public void Flush() { }
        public void Dispose() { }
    }
}
