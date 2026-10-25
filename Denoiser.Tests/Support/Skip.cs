namespace Denoiser.Tests.Support;

/// <summary>
/// When STRICT_SKIPS=1, every skip becomes a test failure.
/// CI sets STRICT_SKIPS=1; local developers leave it unset.
/// </summary>
public static class Skip
{
    public static bool Enabled { get; } =
        Environment.GetEnvironmentVariable("STRICT_SKIPS") == "1";

    public static bool Missing(string envVarName, string? value)
    {
        if (!string.IsNullOrEmpty(value)) return false;
        Fail($"{envVarName} not set");
        return true;
    }

    public static bool MissingFile(string path)
    {
        if (File.Exists(path)) return false;
        Fail($"fixture missing: {path}");
        return true;
    }

    public static void Unless(bool condition, string reason)
    {
        if (condition) return;
        Fail(reason);
    }

    private static void Fail(string reason)
    {
        if (Enabled)
            throw new Xunit.Sdk.XunitException($"[STRICT_SKIPS] {reason}");
        Console.WriteLine($"[SKIP] {reason}");
    }
}
