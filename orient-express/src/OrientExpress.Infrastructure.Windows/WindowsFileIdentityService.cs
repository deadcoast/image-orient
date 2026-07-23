using System.Globalization;
using System.Security.Cryptography;
using OrientExpress.Application.Filesystem;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Infrastructure.Windows.Native;

namespace OrientExpress.Infrastructure.Windows;

/// <summary>
/// Handle-based identity and fingerprints (COMP-FILESYSTEM). Reparse points
/// and cloud placeholders are reported, never traversed or hydrated
/// (DOC-FSSAFE defaults). File IDs are pre-commit identity only: SPIKE-006
/// proved they do not survive ReplaceFileW.
/// </summary>
public sealed class WindowsFileIdentityService : IFileIdentityService
{
    /// <summary>Prefix digest window; catches content drift when size and timestamp are unchanged.</summary>
    public const int PrefixDigestBytes = 128 * 1024;

    public ValueTask<Result<FileSnapshot>> CaptureAsync(
        NormalizedPath path, FingerprintStrength strength, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ValueTask.FromResult(Capture(path, strength, cancellationToken));
        }
        catch (FileNotFoundException)
        {
            return ValueTask.FromResult(Result.Fail<FileSnapshot>(
                "FILE_NOT_FOUND", ErrorCategory.NotFound, false, $"no file at '{path.Display}'"));
        }
        catch (DirectoryNotFoundException)
        {
            return ValueTask.FromResult(Result.Fail<FileSnapshot>(
                "FILE_NOT_FOUND", ErrorCategory.NotFound, false, $"no file at '{path.Display}'"));
        }
        catch (UnauthorizedAccessException)
        {
            return ValueTask.FromResult(Result.Fail<FileSnapshot>(
                "ACCESS_DENIED", ErrorCategory.AccessDenied, false, $"access denied to '{path.Display}'"));
        }
        catch (IOException exception) when ((uint)exception.HResult == 0x80070020)
        {
            return ValueTask.FromResult(Result.Fail<FileSnapshot>(
                "SHARING_VIOLATION", ErrorCategory.SharingViolation, true,
                $"'{path.Display}' is locked by another process"));
        }
        catch (IOException exception)
        {
            return ValueTask.FromResult(Result.Fail<FileSnapshot>(
                "IO_ERROR", ErrorCategory.Unexpected, true, exception.Message));
        }
    }

    public async ValueTask<Result<SnapshotComparison>> CompareAsync(
        FileSnapshot expected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var current = await CaptureAsync(expected.Path, expected.Fingerprint.Strength, cancellationToken)
            .ConfigureAwait(false);
        if (!current.IsSuccess)
        {
            return current.Error!.Category == ErrorCategory.NotFound
                ? Result.Ok(new SnapshotComparison(false, ["MISSING"]))
                : Result.Fail<SnapshotComparison>(current.Error!);
        }

        var observed = current.Require();
        var reasons = new List<string>();
        AddIf(reasons, observed.VolumeSerialHex != expected.VolumeSerialHex, "VOLUME_CHANGED");
        AddIf(reasons, observed.FileIdHex != expected.FileIdHex, "FILE_ID_CHANGED");
        AddIf(reasons, observed.Fingerprint.SizeBytes != expected.Fingerprint.SizeBytes, "SIZE_CHANGED");
        AddIf(reasons, observed.Fingerprint.LastWriteUtc != expected.Fingerprint.LastWriteUtc, "LAST_WRITE_CHANGED");
        AddIf(reasons, observed.Fingerprint.PrefixSha256 != expected.Fingerprint.PrefixSha256, "CONTENT_PREFIX_CHANGED");
        AddIf(
            reasons,
            expected.Fingerprint.FullSha256 is not null
                && observed.Fingerprint.FullSha256 != expected.Fingerprint.FullSha256,
            "CONTENT_CHANGED");
        return Result.Ok(new SnapshotComparison(reasons.Count == 0, reasons));
    }

    private static Result<FileSnapshot> Capture(
        NormalizedPath path, FingerprintStrength strength, CancellationToken cancellationToken)
    {
        // Attribute pre-check without following links: reparse points and
        // cloud placeholders are policy-refused before any content read.
        var attributes = File.GetAttributes(path.Extended);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return Result.Fail<FileSnapshot>(
                "REPARSE_NOT_TRAVERSED", ErrorCategory.CapabilityUnavailable, false,
                $"'{path.Display}' is a reparse point; traversal is disabled by policy");
        }

        using var stream = new FileStream(
            path.Extended, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!NativeMethods.GetFileInformationByHandle(stream.SafeFileHandle, out var information))
        {
            return Result.Fail<FileSnapshot>(
                "IDENTITY_QUERY_FAILED", ErrorCategory.Unexpected, true,
                $"GetFileInformationByHandle failed for '{path.Display}'");
        }

        if ((information.FileAttributes
            & (NativeMethods.AttributeOffline | NativeMethods.AttributeRecallOnOpen | NativeMethods.AttributeRecallOnDataAccess)) != 0)
        {
            return Result.Fail<FileSnapshot>(
                "CLOUD_PLACEHOLDER_NOT_HYDRATED", ErrorCategory.CapabilityUnavailable, false,
                $"'{path.Display}' is an offline/cloud placeholder; hydration is disabled by policy");
        }

        _ = NativeMethods.TryGetVolumeInformation(stream.SafeFileHandle, out var fileSystem, out var volumeSerial);
        var capability = WindowsVolumeCapabilityProbe.Classify(path, fileSystem, volumeSerial);

        cancellationToken.ThrowIfCancellationRequested();
        var fingerprint = ComputeFingerprint(stream, information, strength, cancellationToken);
        var fileId = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return Result.Ok(new FileSnapshot(
            Path: path,
            VolumeSerialHex: information.VolumeSerialNumber.ToString("x8", CultureInfo.InvariantCulture),
            FileIdHex: fileId.ToString("x16", CultureInfo.InvariantCulture),
            LinkCount: information.NumberOfLinks,
            IsReparsePoint: false,
            IsCloudPlaceholder: false,
            IsOffline: false,
            Fingerprint: fingerprint,
            Capability: capability));
    }

    private static FileFingerprint ComputeFingerprint(
        FileStream stream,
        NativeMethods.ByHandleFileInformation information,
        FingerprintStrength strength,
        CancellationToken cancellationToken)
    {
        var lastWrite = DateTimeOffset.FromFileTime(
            (long)(((ulong)information.LastWriteTimeHigh << 32) | information.LastWriteTimeLow));

        stream.Position = 0;
        var prefixLength = (int)Math.Min(stream.Length, PrefixDigestBytes);
        var prefix = new byte[prefixLength];
        stream.ReadExactly(prefix, 0, prefixLength);
        var prefixSha = Convert.ToHexStringLower(SHA256.HashData(prefix));

        string? fullSha = null;
        if (strength == FingerprintStrength.Strong)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stream.Position = 0;
            fullSha = Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        return new FileFingerprint(
            strength,
            stream.Length,
            lastWrite.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            prefixSha,
            fullSha);
    }

    private static void AddIf(List<string> reasons, bool condition, string reason)
    {
        if (condition)
        {
            reasons.Add(reason);
        }
    }
}
