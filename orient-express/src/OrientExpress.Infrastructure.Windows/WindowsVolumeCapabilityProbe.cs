using System.Globalization;
using OrientExpress.Application.Filesystem;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Infrastructure.Windows.Native;

namespace OrientExpress.Infrastructure.Windows;

/// <summary>
/// NFR-COMP-001 capability classification: NTFS on a local fixed volume is
/// the supported class; everything else is honestly gated, never assumed.
/// </summary>
public sealed class WindowsVolumeCapabilityProbe : IVolumeCapabilityProbe
{
    public Result<FilesystemCapability> Probe(NormalizedPath directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!Directory.Exists(directory.Extended))
        {
            return Result.Fail<FilesystemCapability>(
                "DIRECTORY_NOT_FOUND", ErrorCategory.NotFound, false,
                $"no directory at '{directory.Display}'");
        }

        // A short-lived DeleteOnClose file inside the target directory gives a
        // handle on the right volume without leaving anything behind.
        var fileSystem = "unknown";
        uint serial = 0;
        using (var probeStream = CreateProbeStream(directory))
        {
            if (probeStream is not null)
            {
                _ = NativeMethods.TryGetVolumeInformation(probeStream.SafeFileHandle, out fileSystem, out serial);
            }
        }

        return Result.Ok(Classify(directory, fileSystem, serial));
    }

    internal static FilesystemCapability Classify(NormalizedPath path, string fileSystem, uint volumeSerial)
    {
        var driveType = DriveType.Unknown;
        var root = Path.GetPathRoot(path.Display);
        if (!string.IsNullOrEmpty(root) && !root.StartsWith(@"\\", StringComparison.Ordinal))
        {
            try
            {
                driveType = new DriveInfo(root).DriveType;
            }
            catch (ArgumentException)
            {
                driveType = DriveType.Unknown;
            }
        }
        else if (root?.StartsWith(@"\\", StringComparison.Ordinal) == true)
        {
            driveType = DriveType.Network;
        }

        var capabilityClass = (fileSystem, driveType) switch
        {
            ("NTFS", DriveType.Fixed) => FilesystemCapabilityClass.SupportedNtfs,
            (_, DriveType.Network) => FilesystemCapabilityClass.GatedNetwork,
            (_, DriveType.Removable) => FilesystemCapabilityClass.GatedRemovable,
            ("NTFS", _) => FilesystemCapabilityClass.GatedUnknown,
            ("unknown", _) => FilesystemCapabilityClass.GatedUnknown,
            _ => FilesystemCapabilityClass.GatedNonNtfs,
        };
        return new FilesystemCapability(
            capabilityClass,
            fileSystem,
            volumeSerial.ToString("x8", CultureInfo.InvariantCulture),
            driveType.ToString());
    }

    private static FileStream? CreateProbeStream(NormalizedPath directory)
    {
        try
        {
            var candidate = Path.Combine(
                directory.Extended, "upright-volprobe-" + Guid.NewGuid().ToString("N") + ".tmp");
            return new FileStream(
                candidate, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 4096,
                FileOptions.DeleteOnClose);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
