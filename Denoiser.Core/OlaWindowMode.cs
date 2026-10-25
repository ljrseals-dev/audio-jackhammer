namespace Denoiser.Core;

/// <summary>
/// Synthesis window applied by OverlapAddAdapter. The product of this window
/// and the inner model's analysis window must satisfy the COLA condition.
/// Note: DeepFilterNet does its own STFT/ISTFT and must NOT be wrapped in OLA.
/// </summary>
public enum OlaWindowMode
{
    /// <summary>sqrt-Hann. Pairs with models that apply sqrt-Hann analysis.</summary>
    SqrtHann,
    /// <summary>Hann. Pairs with models that apply rectangular analysis.</summary>
    Hann,
    /// <summary>Rectangular. Pairs with models that apply Hann analysis.</summary>
    Rectangular,
    /// <summary>Vorbis (Princen-Bradley). For future models that need external OLA with Vorbis analysis.</summary>
    Vorbis,
}
