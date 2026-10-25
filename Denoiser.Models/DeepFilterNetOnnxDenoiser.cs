using Denoiser.Core;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Denoiser.Models;

/// <summary>
/// DeepFilterNet2-style ONNX denoiser.
///
/// Model placement:
///   Place the exported .onnx file at the path given to the constructor
///   (default: models/deepfilternet2.onnx relative to the working directory).
///
/// Frame size / hop size are read from model metadata when available;
/// defaults assume 480-sample hop (10 ms @ 48 kHz) for compatibility.
///
/// If the ONNX file is missing, construction throws with an actionable message.
/// </summary>
public sealed class DeepFilterNetOnnxDenoiser : IDenoiser
{
    public int FrameSize { get; }
    public int SampleRate => 48000;
    public string Name => "deepfilternet";

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string _outputName;
    private bool _disposed;

    public DeepFilterNetOnnxDenoiser(string modelPath = "models/deepfilternet2.onnx", int frameSize = 480)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"DeepFilterNet ONNX model not found at '{modelPath}'. " +
                "Export DeepFilterNet2 to ONNX and place the file there, or pass --model-path. " +
                "See README.md for export instructions.", modelPath);
        }

        var options = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };
        _session = new InferenceSession(modelPath, options);

        _inputName = _session.InputMetadata.Keys.First();
        _outputName = _session.OutputMetadata.Keys.First();
        FrameSize = frameSize;
    }

    public void Process(Span<float> frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int offset = 0;
        while (offset + FrameSize <= frame.Length)
        {
            var chunk = frame.Slice(offset, FrameSize);
            var inputTensor = new DenseTensor<float>(chunk.ToArray(), new[] { 1, FrameSize });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();
            if (output.Length >= FrameSize)
                output.AsSpan(0, FrameSize).CopyTo(chunk);
            offset += FrameSize;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
    }
}
