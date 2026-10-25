using System.Runtime.InteropServices;
using Denoiser.Core;

namespace Denoiser.Models;

/// <summary>
/// RNNoise-based denoiser via P/Invoke to librnnoise.
/// Frame size is fixed at 480 samples (10 ms @ 48 kHz).
///
/// Native library placement:
///   - Linux:   librnnoise.so  in the app directory or LD_LIBRARY_PATH
///   - Windows: rnnoise.dll
///   - macOS:   librnnoise.dylib
///
/// Obtain the library from https://github.com/xiph/rnnoise or a prebuilt package.
/// If the native library is not found, construction throws with an actionable message.
/// </summary>
public sealed class RnNoiseDenoiser : IDenoiser
{
    public const int RnFrameSize = 480;

    public int FrameSize => RnFrameSize;
    public int SampleRate => 48000;
    public string Name => "rnnoise";

    private IntPtr _state;
    private readonly float[] _frameBuf = new float[RnFrameSize];
    private bool _disposed;

    public RnNoiseDenoiser()
    {
        try
        {
            _state = rnnoise_create(IntPtr.Zero);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException(
                "Native RNNoise library not found. Place librnnoise.so (Linux), rnnoise.dll (Windows), " +
                "or librnnoise.dylib (macOS) next to the executable or on the system library path. " +
                "See README.md for build/download instructions.", ex);
        }

        if (_state == IntPtr.Zero)
            throw new InvalidOperationException("rnnoise_create returned null — failed to initialize RNNoise state.");
    }

    public void Process(Span<float> frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Process in 480-sample chunks; remainder is left untouched (caller should pad)
        int offset = 0;
        while (offset + RnFrameSize <= frame.Length)
        {
            var chunk = frame.Slice(offset, RnFrameSize);
            chunk.CopyTo(_frameBuf);
            rnnoise_process_frame(_state, _frameBuf, _frameBuf);
            _frameBuf.AsSpan().CopyTo(chunk);
            offset += RnFrameSize;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_state != IntPtr.Zero)
        {
            rnnoise_destroy(_state);
            _state = IntPtr.Zero;
        }
    }

    // ---- P/Invoke -----------------------------------------------------------

    private const string LibName = "rnnoise";

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr rnnoise_create(IntPtr model);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void rnnoise_destroy(IntPtr state);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern float rnnoise_process_frame(IntPtr state, float[] output, float[] input);
}
