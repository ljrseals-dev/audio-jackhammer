using Denoiser.Core;
using NAudio.Wave;

namespace Denoiser.Codecs;

/// <summary>
/// Captures live mono 48 kHz float32 PCM from the default (or specified) recording device.
/// </summary>
public sealed class MicrophoneSource : IAudioSource
{
    private readonly WaveInEvent _waveIn;
    private readonly BufferedWaveProvider _buffer;
    private readonly float[] _temp;
    private bool _disposed;

    public int SampleRate => 48000;
    public int Channels => 1;
    public TimeSpan? Duration => null;

    public MicrophoneSource(int deviceNumber = 0, int sampleRate = 48000)
    {
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(sampleRate, 16, 1),
            BufferMilliseconds = 50
        };
        _buffer = new BufferedWaveProvider(_waveIn.WaveFormat)
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true
        };
        _temp = new float[4096];
        _waveIn.DataAvailable += (_, e) => _buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);
        _waveIn.StartRecording();
    }

    public int Read(Span<float> buffer)
    {
        // Convert 16-bit PCM from the ring buffer to float32
        int bytesNeeded = buffer.Length * 2;
        byte[] bytes = new byte[bytesNeeded];
        int bytesRead = _buffer.Read(bytes, 0, bytesNeeded);
        int samples = bytesRead / 2;
        for (int i = 0; i < samples; i++)
        {
            short s = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            buffer[i] = s / 32768f;
        }
        return samples;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _waveIn.StopRecording();
        _waveIn.Dispose();
    }
}
