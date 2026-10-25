namespace Denoiser.Models.DeepFilterNet;

/// <summary>
/// Vorbis window satisfying Princen-Bradley: w[i]^2 + w[i+n/2]^2 == 1.
///   w[i] = sin(π/2 * sin²(π*(i+0.5)/n))
/// </summary>
public static class VorbisWindow
{
    public static float[] Create(int n)
    {
        if (n <= 0 || (n & 1) != 0)
            throw new ArgumentException("Vorbis window length must be positive even.", nameof(n));

        var w = new float[n];
        for (int i = 0; i < n; i++)
        {
            double s = Math.Sin(Math.PI * (i + 0.5) / n);
            w[i] = (float)Math.Sin(Math.PI / 2.0 * s * s);
        }
        return w;
    }
}
