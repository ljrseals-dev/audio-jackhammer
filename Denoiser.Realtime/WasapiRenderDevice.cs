using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Denoiser.Realtime;

/// <summary>
/// WASAPI shared-mode render via BufferedWaveProvider + pull thread.
/// Windows only.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WasapiRenderDevice : IAudioRenderDevice
{
    private readonly WasapiOut _output;
    private readonly BufferedWaveProvider _provider;
    private readonly float[] _floatScratch = new float[480];
    private readonly byte[] _byteScratch = new byte[480 * 4];
    private Thread? _pullThread;
    private volatile bool _running;

    public WasapiRenderDevice(int deviceIndex = 0, int latencyMs = 10)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("WASAPI render requires Windows.");

        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        if (deviceIndex < 0 || deviceIndex >= devices.Count)
            throw new ArgumentOutOfRangeException(nameof(deviceIndex));

        var device = devices[deviceIndex];
        _output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync: true, latencyMs);
        var format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        _provider = new BufferedWaveProvider(format)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromMilliseconds(200),
        };
        _output.Init(_provider);
    }

    public int SampleRate => 48000;
    public int Channels => 1;
    public event RenderRequestHandler? RenderRequested;

    public void Start()
    {
        _running = true;
        _pullThread = new Thread(PullLoop)
        {
            Name = "Denoiser.RenderPull",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        _pullThread.Start();
        _output.Play();
    }

    private void PullLoop()
    {
        while (_running)
        {
            try
            {
                _floatScratch.AsSpan().Clear();
                RenderRequested?.Invoke(_floatScratch);
                // float → IEEE bytes
                Buffer.BlockCopy(_floatScratch, 0, _byteScratch, 0, _floatScratch.Length * 4);
                _provider.AddSamples(_byteScratch, 0, _floatScratch.Length * 4);
            }
            catch
            {
                // keep running
            }
            Thread.Sleep(8); // ~10 ms cadence for 480-sample frames @ 48 kHz
        }
    }

    public void Stop()
    {
        _running = false;
        _pullThread?.Join(TimeSpan.FromSeconds(2));
        try { _output.Stop(); } catch { }
    }

    public void Dispose()
    {
        Stop();
        _output.Dispose();
    }

    public static int ListDevices(TextWriter writer)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            writer.WriteLine("WASAPI not available on this platform.");
            return 0;
        }
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
        for (int i = 0; i < devices.Count; i++)
            writer.WriteLine($"  [{i}] {devices[i].FriendlyName}");
        return devices.Count;
    }
}
