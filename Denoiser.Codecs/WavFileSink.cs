using Denoiser.Core;
using NAudio.Wave;

namespace Denoiser.Codecs;

/// <summary>
/// Writes mono 48 kHz float32 PCM to a 16-bit or 32-bit float WAV file.
/// </summary>
public sealed class WavFileSink : IAudioSink
{
    private readonly WaveFileWriter _writer;
    private readonly float[] _temp;
    private bool _disposed;

    public WavFileSink(string path, int sampleRate = 48000, bool float32 = false)
    {
        var format = float32
            ? WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)
            : new WaveFormat(sampleRate, 16, 1);
        _writer = new WaveFileWriter(path, format);
        _temp = new float[4096];
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        int offset = 0;
        while (offset < samples.Length)
        {
            int len = Math.Min(_temp.Length, samples.Length - offset);
            samples.Slice(offset, len).CopyTo(_temp);
            _writer.WriteSamples(_temp, 0, len);
            offset += len;
        }
    }

    public void Flush()
    {
        _writer.Flush();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _writer.Dispose();
    }
}
