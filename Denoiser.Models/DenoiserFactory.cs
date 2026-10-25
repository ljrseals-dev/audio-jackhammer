using Denoiser.Core;
using Denoiser.Models.DeepFilterNet;

namespace Denoiser.Models;

public static class DenoiserFactory
{
    public static IDenoiser Create(string modelName, string? deepFilterModelPath = null, string? auxPath = null, string dfnMode = "full")
    {
        return modelName.ToLowerInvariant() switch
        {
            "null" or "none" or "passthrough" => new NullDenoiser(),
            "rnnoise" => new RnNoiseDenoiser(),
            "deepfilternet" or "dfn" or "deepfilter" =>
                new DeepFilterNetOnnxDenoiser(
                    deepFilterModelPath ?? Environment.GetEnvironmentVariable("DEEP_FILTER_NET_MODEL") ?? "models/deepfilter.onnx",
                    auxPath ?? Environment.GetEnvironmentVariable("DEEP_FILTER_NET_AUX"),
                    mode: dfnMode.Equals("mask-only", StringComparison.OrdinalIgnoreCase)
                        ? DeepFilterNet.DfnMode.MaskOnly
                        : DeepFilterNet.DfnMode.Full),
            _ => throw new ArgumentException(
                $"Unknown model '{modelName}'. Supported: rnnoise, deepfilternet, null.")
        };
    }
}
