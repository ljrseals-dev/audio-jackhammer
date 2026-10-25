using Denoiser.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Denoiser.Codecs;

/// <summary>
/// Streams any NAudio-supported file (WAV, MP3, AAC, FLAC, WMA, Opus via platform codecs)
/// as mono 48 kHz float32 PCM.
/// </summary>
public sealed class AudioFileSource : IAudioSource
{
    private readonly AudioFileReader _reader;
    private readonly ISampleProvider _provider;
    private readonly float[] _tempBuffer;
    private bool _disposed;

    public int SampleRate => 48000;
    public int Channels => 1;
    public TimeSpan? Duration => _reader.TotalTime;

    public AudioFileSource(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Input path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException($"Input file not found: {path}", path);

        _reader = new AudioFileReader(path);
        ISampleProvider source = _reader;

        // Convert to mono if needed
        if (source.WaveFormat.Channels > 1)
            source = new StereoToMonoSampleProvider(source);

        // Resample to 48 kHz if needed
        if (source.WaveFormat.SampleRate != 48000)
            source = new WdlResamplingSampleProvider(source, 48000);

        _provider = source;
        _tempBuffer = new float[4096];
    }

    public int Read(Span<float> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int toRead = Math.Min(_tempBuffer.Length, buffer.Length - total);
            int read = _provider.Read(_tempBuffer, 0, toRead);
            if (read == 0)
                break;
            _tempBuffer.AsSpan(0, read).CopyTo(buffer.Slice(total));
            total += read;
        }
        return total;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _reader.Dispose();
    }
}
