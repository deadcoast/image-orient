namespace OrientExpress.Contracts.Filesystem;

/// <summary>
/// Canonical path forms (NFR-COMP-002): the display form for humans, the
/// extended-length form (`\\?\`) for Win32 calls so long Unicode paths never
/// lose information.
/// </summary>
public sealed record NormalizedPath(string Display, string Extended);

public enum FingerprintStrength
{
    /// <summary>Size, last-write time, and a bounded prefix digest.</summary>
    Standard,

    /// <summary>Standard plus the full SHA-256 (required before destructive apply).</summary>
    Strong,
}

/// <summary>DOC-DATAMODEL: the job records which strength was captured; a weaker fingerprint cannot satisfy a stronger policy.</summary>
public sealed record FileFingerprint(
    FingerprintStrength Strength,
    long SizeBytes,
    string LastWriteUtc,
    string PrefixSha256,
    string? FullSha256);

public enum FilesystemCapabilityClass
{
    /// <summary>NTFS on a local fixed volume — the NFR-COMP-001 primary target.</summary>
    SupportedNtfs,
    GatedNetwork,
    GatedRemovable,
    GatedNonNtfs,
    GatedUnknown,
}

public sealed record FilesystemCapability(
    FilesystemCapabilityClass Class,
    string FileSystemName,
    string VolumeSerialHex,
    string DriveType);

/// <summary>
/// Handle-derived identity plus content fingerprints (DOC-COMPONENTS: paths
/// alone are not identities). SPIKE-006: the file ID does not survive
/// ReplaceFileW, so identity is valid pre-commit only and must be
/// re-captured after any replacement.
/// </summary>
public sealed record FileSnapshot(
    NormalizedPath Path,
    string VolumeSerialHex,
    string FileIdHex,
    uint LinkCount,
    bool IsReparsePoint,
    bool IsCloudPlaceholder,
    bool IsOffline,
    FileFingerprint Fingerprint,
    FilesystemCapability Capability);

public sealed record SnapshotComparison(
    bool Matches,
    IReadOnlyList<string> DriftReasons);
