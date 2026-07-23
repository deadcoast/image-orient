using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;

namespace OrientExpress.Application.Filesystem;

/// <summary>COMP-FILESYSTEM port (DOC-COMPONENTS `IFileIdentityService`).</summary>
public interface IFileIdentityService
{
    public ValueTask<Result<FileSnapshot>> CaptureAsync(
        NormalizedPath path,
        FingerprintStrength strength,
        CancellationToken cancellationToken);

    public ValueTask<Result<SnapshotComparison>> CompareAsync(
        FileSnapshot expected,
        CancellationToken cancellationToken);
}

/// <summary>
/// COMP-NATIVE atomic primitives, qualified by SPIKE-006: write-through
/// ReplaceFileW for existing targets, write-through MoveFileEx only for the
/// create path. No other replacement primitive is permitted.
/// </summary>
public interface IAtomicFileOperations
{
    /// <summary>Replaces an existing target; the replacement file is consumed.</summary>
    public Result<bool> ReplaceExisting(NormalizedPath target, NormalizedPath replacement);

    /// <summary>Moves the staged file to a destination that must not exist yet.</summary>
    public Result<bool> MoveToNewDestination(NormalizedPath source, NormalizedPath destination);
}

/// <summary>NFR-COMP-001 capability classification for a directory's volume.</summary>
public interface IVolumeCapabilityProbe
{
    public Result<FilesystemCapability> Probe(NormalizedPath directory);
}
