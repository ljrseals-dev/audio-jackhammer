using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// Wraps the verified single-graph DeepFilterNet ONNX export.
///   in  feat_erb  [1,1,T,32]
///   in  feat_spec [1,2,T,96]
///   out erb_mask  [1,1,T,32]
///   out df_coefs  [1,5,T,96,2]
/// </summary>
public sealed class DeepFilterNetOnnxSession : IDeepFilterNetOnnxSession
{
    private readonly InferenceSession _session;

    public DeepFilterNetOnnxSession(string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException(
                "DeepFilterNet ONNX model not found. Download deepfilter.onnx " +
                "(single-graph export) and set DEEP_FILTER_NET_MODEL / --model-path.",
                modelPath);

        var opts = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2),
        };
        _session = new InferenceSession(modelPath, opts);
    }

    public (float[] erbMask, float[] dfCoefs) Run(float[] featErb, float[] featSpec, int t)
    {
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("feat_erb",
                new DenseTensor<float>(featErb, new[] { 1, 1, t, 32 })),
            NamedOnnxValue.CreateFromTensor("feat_spec",
                new DenseTensor<float>(featSpec, new[] { 1, 2, t, 96 })),
        };

        using var results = _session.Run(inputs);

        float[]? mask = null;
        float[]? coefs = null;
        foreach (var r in results)
        {
            if (r.Name is "erb_mask" or "m")
                mask = r.AsTensor<float>().ToArray();
            else if (r.Name is "df_coefs" or "coefs")
                coefs = r.AsTensor<float>().ToArray();
        }

        if (mask is null || coefs is null)
            throw new InvalidOperationException(
                "ONNX graph did not produce erb_mask and df_coefs. " +
                $"Produced: {string.Join(", ", results.Select(x => x.Name))}");

        return (mask, coefs);
    }

    public void Dispose() => _session.Dispose();
}
