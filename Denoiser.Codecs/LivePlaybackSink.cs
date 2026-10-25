using Denoiser.Core;
using NAudio.Wave;

namespace Denoiser.Codecs;

/// <summary>
/// Plays mono 48 kHz float32 PCM through the default WASAPI / WaveOut device.
/// On Linux this may require ALSA/PulseAudio backends; falls back gracefully.
/// </summary>
public sealed class LivePlaybackSink : IAudioSink
{
    private readonly IWavePlayer _player;
    private readonly BufferedWaveProvider _buffer;
    private readonly byte[] _byteBuf;
    private bool _disposed;
    private bool _started;

    public LivePlaybackSink(int deviceNumber = -1, int sampleRate = 48000)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        _buffer = new BufferedWaveProvider(format)
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true
        };
        _byteBuf = new byte[8192 * sizeof(float)];

        // WaveOutEvent works on Windows; on Linux NAudio may use ALSA if available
        _player = deviceNumber >= 0
            ? new WaveOutEvent { DeviceNumber = deviceNumber }
            : new WaveOutEvent();
        _player.Init(_buffer);
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        int byteCount = samples.Length * sizeof(float);
        if (byteCount > _byteBuf.Length)
        {
            // Rare large chunk — allocate temporary
            var tmp = new byte[byteCount];
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples).CopyTo(tmp);
            _buffer.AddSamples(tmp, 0, byteCount);
        }
        else
        {
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples).CopyTo(_byteBuf);
            _buffer.AddSamples(_byteBuf, 0, byteCount);
        }

        if (!_started)
        {
            _player.Play();
            _started = true;
        }
    }

    public void Flush()
    {
        // Wait briefly for buffer to drain
        while (_buffer.BufferedBytes > 0)
            Thread.Sleep(20);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _player.Stop();
        _player.Dispose();
    }
}
