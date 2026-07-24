namespace OrientExpress.Infrastructure.Metadata.ExifTool;

/// <summary>
/// Finds the ExifTool binary (DEP-007). The located path is always surfaced
/// through doctor so a substituted binary is visible; explicit configuration
/// takes precedence once WP-018 wires it.
/// </summary>
public static class ExifToolLocator
{
    public static string? Locate(IReadOnlyDictionary<string, string> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);
        var candidates = new List<string>
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "ExifTool", "ExifTool.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "exiftool", "exiftool.exe"),
        };
        if (environmentVariables.TryGetValue("PATH", out var pathValue))
        {
            candidates.AddRange(
                pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(directory => Path.Combine(directory, "exiftool.exe")));
        }

        return candidates.FirstOrDefault(File.Exists);
    }
}
