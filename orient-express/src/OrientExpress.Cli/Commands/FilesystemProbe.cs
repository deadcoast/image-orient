using System.Security.Cryptography;
using OrientExpress.Application.Filesystem;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Reporting;
using OrientExpress.Infrastructure.Windows;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `doctor --probe-target` (PRD-FR-002: filesystem operation probes when an
/// explicit target is supplied). Probes run only on operator-named
/// directories, use unpredictable exclusive-create names, never touch
/// existing files, and always clean up.
/// </summary>
public static class FilesystemProbe
{
    public static IReadOnlyList<ComponentCapability> Run(string targetDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        var results = new List<ComponentCapability>();
        var directory = PathCanonicalizer.Normalize(targetDirectory);

        var capabilityResult = new WindowsVolumeCapabilityProbe().Probe(directory);
        if (!capabilityResult.IsSuccess)
        {
            results.Add(new ComponentCapability
            {
                Id = "probe.volume",
                Available = false,
                Status = $"{capabilityResult.Error!.Code}: {capabilityResult.Error!.SafeMessage}",
            });
            return results;
        }

        var capability = capabilityResult.Require();
        results.Add(new ComponentCapability
        {
            Id = "probe.volume",
            Available = capability.Class == FilesystemCapabilityClass.SupportedNtfs,
            Status = $"{capability.Class}: {capability.FileSystemName} on {capability.DriveType} volume {capability.VolumeSerialHex}",
        });

        var identity = new WindowsFileIdentityService();
        var atomic = new WindowsAtomicFileOperations();
        var probeName = "upright-doctor-probe-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));
        var probePath = PathCanonicalizer.Normalize(Path.Combine(directory.Display, probeName + ".tmp"));
        var movedPath = PathCanonicalizer.Normalize(Path.Combine(directory.Display, probeName + ".moved.tmp"));
        NormalizedPath? stagePath = null;
        try
        {
            using (var stream = new FileStream(probePath.Extended, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write("upright doctor probe (safe to delete)"u8);
                stream.Flush(flushToDisk: true);
            }

            var before = identity.CaptureAsync(probePath, FingerprintStrength.Strong, cancellationToken)
                .AsTask().GetAwaiter().GetResult();
            var comparison = before.IsSuccess
                ? identity.CompareAsync(before.Require(), cancellationToken).AsTask().GetAwaiter().GetResult()
                : default;
            results.Add(before.IsSuccess && comparison.IsSuccess && comparison.Require().Matches
                ? new ComponentCapability
                {
                    Id = "probe.identity",
                    Available = true,
                    Status = $"capture and compare ok; fileId {before.Require().FileIdHex}",
                }
                : new ComponentCapability
                {
                    Id = "probe.identity",
                    Available = false,
                    Status = before.Error?.SafeMessage ?? comparison.Error?.SafeMessage ?? "comparison drifted unexpectedly",
                });

            stagePath = StagePathPlanner.PlanStagePath(probePath);
            using (var stage = new FileStream(stagePath.Extended, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stage.Write("upright doctor probe replacement"u8);
                stage.Flush(flushToDisk: true);
            }

            var replace = atomic.ReplaceExisting(probePath, stagePath);
            var after = identity.CaptureAsync(probePath, FingerprintStrength.Standard, cancellationToken)
                .AsTask().GetAwaiter().GetResult();
            var reBaselined = before.IsSuccess && after.IsSuccess
                && before.Require().FileIdHex != after.Require().FileIdHex;
            results.Add(replace.IsSuccess && after.IsSuccess
                ? new ComponentCapability
                {
                    Id = "probe.atomic-replace",
                    Available = true,
                    Status = reBaselined
                        ? "ReplaceFileW ok; file id re-baselined as SPIKE-006 predicts"
                        : "ReplaceFileW ok",
                }
                : new ComponentCapability
                {
                    Id = "probe.atomic-replace",
                    Available = false,
                    Status = replace.Error?.SafeMessage ?? after.Error?.SafeMessage ?? "replace failed",
                });

            var move = atomic.MoveToNewDestination(probePath, movedPath);
            results.Add(new ComponentCapability
            {
                Id = "probe.create-path",
                Available = move.IsSuccess,
                Status = move.IsSuccess
                    ? "MoveFileEx(write-through) to a new destination ok"
                    : $"{move.Error!.Code}: {move.Error!.SafeMessage}",
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            results.Add(new ComponentCapability
            {
                Id = "probe.write",
                Available = false,
                Status = "probe files could not be created: " + exception.Message,
            });
        }
        finally
        {
            DeleteQuietly(probePath.Extended);
            DeleteQuietly(movedPath.Extended);
            if (stagePath is not null)
            {
                DeleteQuietly(stagePath.Extended);
            }
        }

        return results;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup is best effort; probe files are clearly named and inert.
        }
    }
}
