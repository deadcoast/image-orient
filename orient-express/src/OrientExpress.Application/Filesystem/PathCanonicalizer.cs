using OrientExpress.Contracts.Filesystem;

namespace OrientExpress.Application.Filesystem;

/// <summary>
/// NFR-COMP-002: full canonical form plus the extended-length (`\\?\`) form
/// so Unicode long paths reach Win32 without lossy normalization or the
/// MAX_PATH ceiling. String-pure: no filesystem access.
/// </summary>
public static class PathCanonicalizer
{
    private const string ExtendedPrefix = @"\\?\";
    private const string ExtendedUncPrefix = @"\\?\UNC\";

    public static NormalizedPath Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        return new NormalizedPath(full, ToExtended(full));
    }

    public static string ToExtended(string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        if (fullPath.StartsWith(ExtendedPrefix, StringComparison.Ordinal))
        {
            return fullPath;
        }

        return fullPath.StartsWith(@"\\", StringComparison.Ordinal)
            ? ExtendedUncPrefix + fullPath[2..]
            : ExtendedPrefix + fullPath;
    }
}
