using System.Globalization;
using System.Text.Json;
using OrientExpress.Application.Filesystem;
using OrientExpress.Application.Jobs;
using OrientExpress.Application.Metadata;
using OrientExpress.Application.Pipeline;
using OrientExpress.Application.Transform;
using OrientExpress.Cli.Config;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Jobs;
using OrientExpress.Contracts.Pipeline;
using OrientExpress.Contracts.Transform;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `upright scan <source> --output <dir>` (PRD-FR-007/008, AC-014):
/// read-only planning for the DETERMINISTIC path — only files whose stored
/// EXIF orientation tag (2..8) proves a rotation are planned for
/// normalization. Sideways photos with a clean tag need the ML decision
/// engine, which stays gated (WP-007/008); they are honestly planned Keep
/// here. Seals an immutable plan and reports what it will and will not do.
/// </summary>
public static class ScanCommand
{
    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };

    public static async Task<int> RunAsync(
        OutputContext context,
        ConfigLoadResult config,
        IFileIdentityService identity,
        IImageFormatProber prober,
        IJobStore store,
        string sourceRoot,
        string outputRoot,
        bool recurse,
        string fingerprint,
        int? maxFilesOverride,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(config);
        if (config.Effective is null)
        {
            context.WriteError(
                CliExitCode.InvalidConfiguration, "CONFIG_INVALID",
                "the effective configuration did not validate",
                "run 'upright config validate'");
            return (int)CliExitCode.InvalidConfiguration;
        }

        if (!Directory.Exists(sourceRoot))
        {
            context.WriteError(
                CliExitCode.InvalidArgument, "SOURCE_NOT_FOUND",
                $"'{sourceRoot}' is not an existing directory", "pass an existing folder of photos");
            return (int)CliExitCode.InvalidArgument;
        }

        var strength = fingerprint == "strong" ? FingerprintStrength.Strong : FingerprintStrength.Standard;
        var effective = config.Effective.Config;
        var edgePolicy = OrientationNormalizationPlanner.ParseEdgePolicy(effective.Output!.JpegEdge);
        if (!edgePolicy.IsSuccess)
        {
            context.WriteError(
                CliExitCode.InvalidConfiguration, edgePolicy.Error!.Code,
                edgePolicy.Error.SafeMessage, "fix output.jpegEdge in configuration");
            return (int)CliExitCode.InvalidConfiguration;
        }

        var sourceCanonical = PathCanonicalizer.Normalize(Path.GetFullPath(sourceRoot));
        var outputCanonical = PathCanonicalizer.Normalize(Path.GetFullPath(outputRoot));
        var maxFiles = (int)Math.Min(int.MaxValue, maxFilesOverride ?? effective.Discovery?.MaxFiles ?? 100_000);

        // Canonical ordered set: deterministic across repeated runs (AC-005).
        var files = Directory
            .EnumerateFiles(sourceCanonical.Display, "*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Take(maxFiles)
            .ToList();
        if (files.Count == 0)
        {
            context.WriteError(
                CliExitCode.NothingToDo, "NO_CANDIDATE_FILES",
                $"no .jpg/.jpeg files under '{sourceCanonical.Display}'", "check the path or --no-recurse");
            return (int)CliExitCode.NothingToDo;
        }

        var manifest = new JobManifest
        {
            JobIdValue = OrientExpress.Infrastructure.Sqlite.SqliteJobStore.NewJobId(),
            CreatedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ToolVersion = VersionCommand.ToolVersion,
            Mode = "safe-copy",
            SourceRoot = sourceCanonical.Display,
            OutputRoot = outputCanonical.Display,
            ConfigDigest = config.Effective.Digest,
        };
        var created = await store.CreateAsync(manifest, cancellationToken).ConfigureAwait(false);
        if (!created.IsSuccess)
        {
            context.WriteError(
                CliExitCode.InternalError, created.Error!.Code, created.Error.SafeMessage, "re-run scan");
            return (int)CliExitCode.InternalError;
        }

        var job = created.Require().Id;
        _ = await store.AppendEventsAsync(
            job, [new JobEvent("scan.started", "running")], cancellationToken).ConfigureAwait(false);

        var limits = StageLimits.Sanitize(
            effective.Resources?.DecodeWorkers, effective.Resources?.QueueCapacity);
        var pending = new List<PlannedFile>();
        long keep = 0, normalize = 0, unsupported = 0;

        var summary = await BoundedPipeline.RunAsync(
            Enumerate(files),
            [
                new PipelineStage<ScanItem>(
                    "plan",
                    limits,
                    async (item, ct) =>
                    {
                        var snapshot = await identity.CaptureAsync(
                            PathCanonicalizer.Normalize(item.Path), strength, ct).ConfigureAwait(false);
                        if (!snapshot.IsSuccess)
                        {
                            return Result.Fail<ScanItem>(snapshot.Error!);
                        }

                        var probe = prober.Probe(snapshot.Require().Path);
                        return Result.Ok(item with
                        {
                            Planned = Plan(
                                item.Sequence,
                                snapshot.Require(),
                                probe.IsSuccess ? probe.Require() : null,
                                sourceCanonical.Display,
                                edgePolicy.Require()),
                        });
                    }),
            ],
            async (item, ct) =>
            {
                switch (item.Planned!.Action)
                {
                    case PlanAction.Keep:
                        keep++;
                        break;
                    case PlanAction.Normalize:
                        normalize++;
                        break;
                    default:
                        unsupported++;
                        break;
                }

                pending.Add(item.Planned);
                if (pending.Count >= 200)
                {
                    _ = (await store.UpsertScanBatchAsync(job, pending, ct).ConfigureAwait(false)).Require();
                    pending.Clear();
                }
            },
            NullProgressSink.Instance,
            cancellationToken,
            CancellationToken.None);

        if (pending.Count > 0)
        {
            _ = (await store.UpsertScanBatchAsync(job, pending, cancellationToken).ConfigureAwait(false)).Require();
        }

        var seal = await store.SealPlanAsync(job, cancellationToken).ConfigureAwait(false);
        if (!seal.IsSuccess)
        {
            context.WriteError(CliExitCode.InternalError, seal.Error!.Code, seal.Error.SafeMessage, "re-run scan");
            return (int)CliExitCode.InternalError;
        }

        _ = await store.AppendEventsAsync(
            job,
            [new JobEvent("scan.sealed", "succeeded", DataJson: $"{{\"rows\":{seal.Require().RowCount}}}")],
            cancellationToken).ConfigureAwait(false);

        var report = new
        {
            command = "scan",
            jobId = job.Value,
            mode = "safe-copy",
            sourceRoot = sourceCanonical.Display,
            outputRoot = outputCanonical.Display,
            planSeal = seal.Require().Sha256,
            counts = new
            {
                planned = seal.Require().RowCount,
                normalize,
                keep,
                unsupported,
                readErrors = summary.Failed,
            },
            deterministicScopeNote =
                "only files whose EXIF orientation tag proves a rotation are normalized; "
                + "sideways photos with a clean tag await the gated ML decision engine (WP-007/008)",
        };
        await File.WriteAllTextAsync(
            Path.Combine(created.Require().JobDirectory, "reports", "scan-summary.json"),
            JsonSerializer.Serialize(report, ReportJson) + "\n",
            cancellationToken).ConfigureAwait(false);

        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, report);
        }
        else
        {
            context.Out.WriteLine($"scan {job.Value} [safe-copy] sealed {seal.Require().RowCount} planned rows");
            context.Out.WriteLine($"  will rotate (EXIF tag proves it) : {normalize}");
            context.Out.WriteLine($"  already upright per tag          : {keep}");
            context.Out.WriteLine($"  unsupported (recorded, skipped)  : {unsupported}");
            context.Out.WriteLine($"  unreadable (recorded, skipped)   : {summary.Failed}");
            context.Out.WriteLine($"  next: upright apply {job.Value}");
        }

        return (int)CliExitCode.Success;
    }

    private sealed record ScanItem(long Sequence, string Path, PlannedFile? Planned = null);

    private async static IAsyncEnumerable<ScanItem> EnumerateCore(
        IReadOnlyList<string> files, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var index = 0; index < files.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            yield return new ScanItem(index + 1, files[index]);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static IAsyncEnumerable<ScanItem> Enumerate(IReadOnlyList<string> files) => EnumerateCore(files);

    private static PlannedFile Plan(
        long sequence,
        Contracts.Filesystem.FileSnapshot snapshot,
        Contracts.Imaging.ImageFormatProbe? probe,
        string sourceRoot,
        JpegEdgePolicy edgePolicy)
    {
        var relative = Path.GetRelativePath(sourceRoot, snapshot.Path.Display);
        var common = new PlannedFile
        {
            Sequence = sequence,
            SourcePath = snapshot.Path.Display,
            SizeBytes = snapshot.Fingerprint.SizeBytes,
            LastWriteUtc = snapshot.Fingerprint.LastWriteUtc,
            PrefixSha256 = snapshot.Fingerprint.PrefixSha256,
            FullSha256 = snapshot.Fingerprint.FullSha256,
            VolumeSerialHex = snapshot.VolumeSerialHex,
            FileIdHex = snapshot.FileIdHex,
            Format = probe?.Format ?? "unknown",
            Width = probe?.Width ?? 0,
            Height = probe?.Height ?? 0,
            ExifOrientation = probe?.ExifOrientation ?? 0,
            Action = PlanAction.Unsupported,
            Operation = JpegOperation.None,
            EdgePolicy = edgePolicy,
            OutputToken = relative,
            ExpectedWidth = probe?.Width ?? 0,
            ExpectedHeight = probe?.Height ?? 0,
            ExpectedOrientation = 1,
        };

        if (probe is null || !string.Equals(probe.Format, "jpeg", StringComparison.Ordinal))
        {
            return common with { ReasonCode = probe is null ? "PROBE_FAILED" : "FORMAT_UNSUPPORTED" };
        }

        var orientation = probe.ExifOrientation ?? 1;
        if (orientation == 1)
        {
            return common with { Action = PlanAction.Keep };
        }

        if (probe.Width is null || probe.Height is null)
        {
            return common with { ReasonCode = "DIMENSIONS_UNREADABLE" };
        }

        var plan = OrientationNormalizationPlanner.FromExifOrientation(
            orientation, probe.Width.Value, probe.Height.Value, edgePolicy);
        if (!plan.IsSuccess)
        {
            return common with { ReasonCode = plan.Error!.Code };
        }

        return common with
        {
            Action = PlanAction.Normalize,
            Operation = plan.Require().Operation,
            ExpectedWidth = plan.Require().ExpectedWidth,
            ExpectedHeight = plan.Require().ExpectedHeight,
            ExpectedOrientation = 1,
        };
    }
}
