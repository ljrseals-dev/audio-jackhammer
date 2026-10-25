namespace Denoiser.Core;

/// <summary>
/// Marker for denoisers with pending tail state at end-of-stream (e.g. OLA adapters).
/// </summary>
public interface IFlushableDenoiser : IDenoiser
{
    /// <summary>
    /// Writes the pending tail into <paramref name="destination"/> and clears state.
    ///
    /// Returns the number of valid samples written (0 ≤ n ≤ destination.Length).
    /// May be less than destination.Length; the caller MUST trim to the returned count.
    ///
    /// Idempotent: calling Flush a second time before any new Process returns 0.
    /// Process may be called again after Flush to begin a new stream (resets state).
    /// </summary>
    int Flush(Span<float> destination);
}
