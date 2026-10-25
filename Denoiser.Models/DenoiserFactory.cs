using Denoiser.Core;

namespace Denoiser.Models;

public static class DenoiserFactory
{
    /// <summary>
    /// Creates an <see cref="IDenoiser"/> by name.
    /// Throws with an actionable message if the requested model cannot be loaded.
    /// </summary>
    public static IDenoiser Create(string modelName, string? deepFilterModelPath = null)
    {
        return modelName.ToLowerInvariant() switch
        {
            "null" or "none" or "passthrough" => new NullDenoiser(),
            "rnnoise" => new RnNoiseDenoiser(),
            "deepfilternet" or "dfn" or "deepfilter" =>
                new DeepFilterNetOnnxDenoiser(deepFilterModelPath ?? "models/deepfilternet2.onnx"),
            _ => throw new ArgumentException(
                $"Unknown model '{modelName}'. Supported: rnnoise, deepfilternet, null.")
        };
    }
}
