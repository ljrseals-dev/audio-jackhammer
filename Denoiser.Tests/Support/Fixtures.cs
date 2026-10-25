using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Denoiser.Tests.Support;

public static class Fixtures
{
    /// <summary>
    /// Resolves Fixtures/ relative to the test project (works under dotnet test).
    /// </summary>
    public static string Root
    {
        get
        {
            // AppContext.BaseDirectory is typically bin/Debug/net8.0/
            var candidates = new[]
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Fixtures")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Fixtures")),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Denoiser.Tests", "Fixtures")),
            };
            foreach (var c in candidates)
                if (Directory.Exists(c)) return c;
            return candidates[0];
        }
    }

    public static string PathOf(string fileName) =>
        System.IO.Path.Combine(Root, fileName);

    /// <summary>
    /// Returns the fixture path if present; otherwise prints a skip line and returns null.
    /// </summary>
    public static string? Require(string fileName)
    {
        var p = PathOf(fileName);
        if (File.Exists(p)) return p;
        Console.WriteLine($"[SKIP] fixture missing: {fileName} (see Fixtures/README.md)");
        return null;
    }

    public static float[] LoadMono48k(string path)
    {
        using var reader = new AudioFileReader(path);
        ISampleProvider sp = reader;
        if (reader.WaveFormat.Channels > 1)
            sp = new StereoToMonoSampleProvider(sp);
        if (sp.WaveFormat.SampleRate != 48000)
            sp = new WdlResamplingSampleProvider(sp, 48000);

        var output = new List<float>(48000 * 10);
        var buf = new float[4096];
        int n;
        while ((n = sp.Read(buf, 0, buf.Length)) > 0)
            output.AddRange(buf.AsSpan(0, n).ToArray());
        return output.ToArray();
    }

    public static void WriteWav(string path, float[] samples, int sampleRate = 48000)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        using var writer = new WaveFileWriter(path,
            WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1));
        writer.WriteSamples(samples, 0, samples.Length);
    }
}
