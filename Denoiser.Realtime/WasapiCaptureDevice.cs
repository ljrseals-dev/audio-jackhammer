using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Denoiser.Realtime;

/// <summary>
/// WASAPI capture → 48 kHz mono float callbacks.
/// Windows only. On other platforms construction throws PlatformNotSupportedException.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WasapiCaptureDevice : IAudioCaptureDevice
{
    private readonly WasapiCapture _capture;
    private readonly float[] _floatScratch;
    private readonly float[] _monoScratch;
    private readonly int _channels;
    private readonly int _bytesPerSample;

    public WasapiCaptureDevice(int deviceIndex = 0, int bufferMs = 10)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("WASAPI capture requires Windows.");

        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        if (deviceIndex < 0 || deviceIndex >= devices.Count)
            throw new ArgumentOutOfRangeException(nameof(deviceIndex),
                $"Capture device index {deviceIndex} out of range (0..{devices.Count - 1}).");

        var device = devices[deviceIndex];
        _capture = new WasapiCapture(device, useEventSync: true, audioBufferMillisecondsLength: bufferMs);
        var fmt = _capture.WaveFormat;
        _channels = fmt.Channels;
        _bytesPerSample = fmt.BitsPerSample / 8;
        if (fmt.SampleRate != 48000)
            throw new NotSupportedException(
                $"Capture device runs at {fmt.SampleRate} Hz; C requires 48 kHz native.");

        // Pre-allocate worst-case 100 ms of floats
        int maxSamples = fmt.SampleRate * fmt.Channels / 10;
        _floatScratch = new float[maxSamples];
        _monoScratch = new float[maxSamples];

        _capture.DataAvailable += OnData;
    }

    public int SampleRate => 48000;
    public int Channels => 1;
    public event CaptureDataHandler? DataAvailable;

    private void OnData(object? sender, WaveInEventArgs e)
    {
        try
        {
            int bytes = e.BytesRecorded;
            if (bytes <= 0) return;

            int sampleCount = bytes / _bytesPerSample;
            int frames = sampleCount / _channels;
            if (frames <= 0) return;

            // Convert to float (IEEE float32 device is common with WasapiCapture)
            var fmt = _capture.WaveFormat;
            if (fmt.Encoding == WaveFormatEncoding.IeeeFloat && fmt.BitsPerSample == 32)
            {
                Buffer.BlockCopy(e.Buffer, 0, _floatScratch, 0, bytes);
            }
            else if (fmt.BitsPerSample == 16)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    short s = (short)(e.Buffer[i * 2] | (e.Buffer[i * 2 + 1] << 8));
                    _floatScratch[i] = s / 32768f;
                }
            }
            else
            {
                return; // unsupported format this callback
            }

            // Mix to mono
            if (_channels == 1)
            {
                DataAvailable?.Invoke(_floatScratch.AsSpan(0, frames));
            }
            else
            {
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0;
                    for (int c = 0; c < _channels; c++)
                        sum += _floatScratch[f * _channels + c];
                    _monoScratch[f] = sum / _channels;
                }
                DataAvailable?.Invoke(_monoScratch.AsSpan(0, frames));
            }
        }
        catch
        {
            // Never throw from callback
        }
    }

    public void Start() => _capture.StartRecording();
    public void Stop() => _capture.StopRecording();
    public void Dispose() => _capture.Dispose();

    public static int ListDevices(TextWriter writer)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            writer.WriteLine("WASAPI not available on this platform.");
            return 0;
        }
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        for (int i = 0; i < devices.Count; i++)
            writer.WriteLine($"  [{i}] {devices[i].FriendlyName}");
        return devices.Count;
    }
}
