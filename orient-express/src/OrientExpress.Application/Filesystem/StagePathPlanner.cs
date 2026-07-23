using System.Security.Cryptography;
using OrientExpress.Contracts.Filesystem;

namespace OrientExpress.Application.Filesystem;

/// <summary>
/// DOC-FSSAFE temporary-file rules: stage files live in the source's own
/// directory (same volume as the commit target), carry an unpredictable
/// suffix, and are exclusively created by the caller. Orphan cleanup keys on
/// journal ownership, never on the marker alone.
/// </summary>
public static class StagePathPlanner
{
    public const string StageMarker = ".oxstage-";

    public static NormalizedPath PlanStagePath(NormalizedPath source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
        return new NormalizedPath(
            source.Display + StageMarker + suffix,
            source.Extended + StageMarker + suffix);
    }

    public static bool LooksLikeStageFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return fileName.Contains(StageMarker, StringComparison.Ordinal);
    }
}
