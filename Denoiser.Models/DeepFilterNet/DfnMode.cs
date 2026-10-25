namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// Selects whether the deep filter is applied on top of the ERB mask.
/// MaskOnly is diagnostic only — do not ship as production default.
/// </summary>
public enum DfnMode
{
    Full,
    MaskOnly,
}
