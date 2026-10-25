using System.Numerics;
using Denoiser.Core;

namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// Full DeepFilterNet: STFT → features → pad_feat → ONNX → 5-tap DF + ERB mask → ISTFT.
/// RequiresOverlapAdd = false.
/// </summary>
public sealed class DeepFilterNetOnnxDenoiser : IFlushableDenoiser
{
    public const int HopSize = 480;

    private readonly IDeepFilterNetOnnxSession _session;
    private readonly DeepFilterNetStft _stft;
    private readonly IErbFilterbank _erb;
    private readonly IDeepFilterKernel _kernel;
    private readonly StreamingNormalizer _norm;
    private readonly DfnMode _mode;

    private readonly List<float[]> _erbHistory = new();
    private readonly List<float[]> _specHistory = new();
    private readonly Queue<FrameState> _coefQueue = new();

    private readonly Complex[] _noisySpec = new Complex[DeepFilterNetStft.Bins];
    private readonly float[] _erbFrame = new float[ErbFilterbank.Bands];
    private readonly float[] _specFeat = new float[2 * DeepFilterKernel.DfBins];
    private readonly float[] _outputHop = new float[HopSize];
    private readonly float[] _binGain = new float[DeepFilterNetStft.Bins];

    private bool _disposed;
    private bool _flushed;

    private readonly record struct FrameState(float[] Mask, float[] Coefs, Complex[] NoisySpec);

    public DeepFilterNetOnnxDenoiser(
        string modelPath,
        string? auxiliaryBinPath = null,
        IFft? fft = null,
        DfnMode mode = DfnMode.Full)
        : this(new DeepFilterNetOnnxSession(modelPath), new ErbFilterbank(auxiliaryBinPath), null, fft, mode)
    {
    }

    /// <summary>Testing constructor — inject fakes without ONNX weights.</summary>
    internal DeepFilterNetOnnxDenoiser(
        IDeepFilterNetOnnxSession session,
        IErbFilterbank erb,
        IDeepFilterKernel? kernel = null,
        IFft? fft = null,
        DfnMode mode = DfnMode.Full)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _erb = erb ?? throw new ArgumentNullException(nameof(erb));
        _kernel = kernel ?? new DeepFilterKernelImpl();
        _stft = new DeepFilterNetStft(fft);
        _norm = new StreamingNormalizer();
        _mode = mode;
    }

    public int FrameSize => HopSize;
    public int SampleRate => 48000;
    public string Name => "deepfilternet";
    public bool RequiresOverlapAdd => false;

    public void Process(Span<float> frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_flushed)
            throw new InvalidOperationException("Process after Flush.");
        if (frame.Length != HopSize)
            throw new ArgumentException($"Expected {HopSize} samples.", nameof(frame));

        var spectrum = _stft.Analyze(frame);
        spectrum.CopyTo(_noisySpec);

        _erb.Forward(spectrum, _erbFrame);
        var erbCopy = (float[])_erbFrame.Clone();
        _norm.NormalizeErb(erbCopy);
        _norm.NormalizeSpec(spectrum, _specFeat);

        _erbHistory.Add(erbCopy);
        _specHistory.Add((float[])_specFeat.Clone());

        int t = _erbHistory.Count;
        var featErbRaw = Flatten(_erbHistory, 32);
        var featSpecRaw = Flatten(_specHistory, 2 * DeepFilterKernel.DfBins);
        var featErb = new float[featErbRaw.Length];
        var featSpec = new float[featSpecRaw.Length];
        PadFeat.ApplyBatch(featErbRaw, featErb, t, 32);
        PadFeat.ApplyBatch(featSpecRaw, featSpec, t, 2 * DeepFilterKernel.DfBins);

        var (mask, coefs) = _session.Run(featErb, featSpec, t);

        var maskSlice = new float[32];
        Array.Copy(mask, (t - 1) * 32, maskSlice, 0, 32);

        var coefSlice = new float[DeepFilterKernel.Order * DeepFilterKernel.DfBins * 2];
        for (int o = 0; o < DeepFilterKernel.Order; o++)
        {
            for (int k = 0; k < DeepFilterKernel.DfBins; k++)
            {
                int src = (((o * t) + (t - 1)) * DeepFilterKernel.DfBins + k) * 2;
                int dst = (o * DeepFilterKernel.DfBins + k) * 2;
                if (src + 1 < coefs.Length)
                {
                    coefSlice[dst] = coefs[src];
                    coefSlice[dst + 1] = coefs[src + 1];
                }
            }
        }

        _coefQueue.Enqueue(new FrameState(maskSlice, coefSlice, (Complex[])_noisySpec.Clone()));

        if (_coefQueue.Count < DeepFilterKernel.Order)
        {
            frame.Clear();
            return;
        }

        EmitCenter(frame);

        const int MaxHistory = 64;
        while (_erbHistory.Count > MaxHistory)
        {
            _erbHistory.RemoveAt(0);
            _specHistory.RemoveAt(0);
        }
    }

    public int Flush(Span<float> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _flushed = true;
        int written = 0;

        while (_coefQueue.Count > DeepFilterKernel.Lookahead && written + HopSize <= destination.Length)
        {
            while (_coefQueue.Count < DeepFilterKernel.Order)
            {
                _coefQueue.Enqueue(new FrameState(
                    new float[32],
                    new float[DeepFilterKernel.Order * DeepFilterKernel.DfBins * 2],
                    new Complex[DeepFilterNetStft.Bins]));
            }

            var hop = destination.Slice(written, HopSize);
            hop.Clear();
            EmitCenter(hop);
            written += HopSize;
        }
        return written;
    }

    private void EmitCenter(Span<float> outputFrame)
    {
        var noisyWindow = new Complex[DeepFilterKernel.Order * DeepFilterNetStft.Bins];
        int w = 0;
        foreach (var item in _coefQueue)
        {
            if (w >= noisyWindow.Length) break;
            item.NoisySpec.CopyTo(noisyWindow.AsSpan(w));
            w += DeepFilterNetStft.Bins;
        }

        var emit = _coefQueue.ElementAt(DeepFilterKernel.Lookahead);

        _erb.Inverse(emit.Mask, _binGain);
        var masked = new Complex[DeepFilterNetStft.Bins];
        for (int k = 0; k < DeepFilterNetStft.Bins; k++)
            masked[k] = emit.NoisySpec[k] * _binGain[k];

        if (_mode == DfnMode.Full)
            _kernel.Apply(noisyWindow, emit.Coefs, masked);
        // MaskOnly: leave ERB-masked spectrum unchanged

        Array.Clear(_outputHop);
        _stft.Synthesize(masked, _outputHop);
        _outputHop.CopyTo(outputFrame);

        _coefQueue.Dequeue();
    }

    private static float[] Flatten(List<float[]> frames, int frameLen)
    {
        var result = new float[frames.Count * frameLen];
        for (int i = 0; i < frames.Count; i++)
            Array.Copy(frames[i], 0, result, i * frameLen, frameLen);
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
    }
}
