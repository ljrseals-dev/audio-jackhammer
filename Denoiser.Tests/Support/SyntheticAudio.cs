namespace Denoiser.Tests.Support;

public static class SyntheticAudio
{
    /// <summary>
    /// Deterministic mixed signal for golden regression.
    /// 2s @ 48 kHz mono: 440 Hz + 660 Hz sine at -6 dBFS + white noise at -30 dBFS,
    /// noise seeded with System.Random(42). Same bytes every run.
    /// </summary>
    public static float[] Generate2s440_660_Seed42()
    {
        const int sampleRate = 48000;
        const int n = sampleRate * 2;
        var output = new float[n];
        var rng = new Random(42);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)sampleRate;
            double s = 0.5 * Math.Sin(2 * Math.PI * 440 * t)
                     + 0.5 * Math.Sin(2 * Math.PI * 660 * t);
            double noise = (rng.NextDouble() * 2 - 1) * 0.032;
            output[i] = (float)(s * 0.5 + noise);
        }
        return output;
    }

    /// <summary>
    /// Synthetic construction-like sample: 0–1 s noise only, 1–2 s speech-like + noise.
    /// Self-generated (CC0). Not a substitute for real site audio, but exercises
    /// band-RMS assertions when no licensed fixture is present.
    /// </summary>
    public static float[] GenerateConstructionLike2s()
    {
        const int sr = 48000;
        const int n = sr * 2;
        var output = new float[n];
        var rng = new Random(7);

        for (int i = 0; i < n; i++)
        {
            double t = i / (double)sr;
            // Broadband-ish construction noise: several tones + noise
            double noise = (rng.NextDouble() * 2 - 1) * 0.15;
            noise += 0.08 * Math.Sin(2 * Math.PI * 90 * t);
            noise += 0.06 * Math.Sin(2 * Math.PI * 180 * t);
            noise += 0.04 * Math.Sin(2 * Math.PI * 1200 * t);
            // Impulsive clicks every ~50 ms
            if (i % 2400 < 20)
                noise += 0.4 * Math.Sin(2 * Math.PI * 3000 * t);

            if (i >= sr)
            {
                // Speech-like formants (formant-ish energy in 300–3400 Hz)
                double speech = 0.25 * Math.Sin(2 * Math.PI * 180 * t)
                              + 0.18 * Math.Sin(2 * Math.PI * 900 * t)
                              + 0.12 * Math.Sin(2 * Math.PI * 1800 * t)
                              + 0.08 * Math.Sin(2 * Math.PI * 2700 * t);
                // Amplitude envelope like syllables
                double env = 0.5 + 0.5 * Math.Sin(2 * Math.PI * 3 * t);
                output[i] = (float)(noise + speech * env);
            }
            else
            {
                output[i] = (float)noise;
            }
        }
        return output;
    }
}
