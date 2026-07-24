using System.Security.Cryptography;

namespace OrientExpress.Infrastructure.Imaging;

/// <summary>
/// Finds the jpegtran binary (DEP-008, libjpeg-turbo). Mirrors the
/// ExifTool locator: well-known install location first, then PATH; the
/// located path and digest surface through doctor so a substituted binary
/// is visible. Explicit configuration takes precedence once WP-018 wires it.
/// </summary>
public static class JpegtranLocator
{
    public static string? Locate(IReadOnlyDictionary<string, string> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);
        var candidates = new List<string>
        {
            @"C:\libjpeg-turbo64\bin\jpegtran.exe",
            @"C:\libjpeg-turbo\bin\jpegtran.exe",
        };
        if (environmentVariables.TryGetValue("PATH", out var pathValue))
        {
            candidates.AddRange(
                pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(directory => Path.Combine(directory, "jpegtran.exe")));
        }

        return candidates.FirstOrDefault(File.Exists);
    }

    public static string Sha256Of(string binaryPath)
    {
        using var stream = File.OpenRead(binaryPath);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
