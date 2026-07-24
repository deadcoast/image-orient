using System.Globalization;
using System.Runtime.InteropServices;
using OrientExpress.Cli.Hosting;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Reporting;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `upright doctor` (PRD-FR-002, AC-003): read-only diagnostics. Emits a
/// capability report conforming to schemas/capability-report.schema.json.
/// Capability truth-telling: a component or format is available only when
/// its gating evidence exists, so today most entries honestly say gated.
/// </summary>
public static class DoctorCommand
{
    private const string NotImplemented = "NOT_YET_IMPLEMENTED";

    public static int Run(
        OutputContext context,
        string? probeTarget,
        OrientExpress.Application.Metadata.IMetadataService metadataService,
        OrientExpress.Application.Inference.IModelRegistry? modelRegistry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(metadataService);
        cancellationToken.ThrowIfCancellationRequested();
        var report = BuildReport(metadataService, modelRegistry);
        if (probeTarget is not null)
        {
            if (!Directory.Exists(probeTarget))
            {
                context.WriteError(
                    CliExitCode.InvalidArgument,
                    "PROBE_TARGET_NOT_FOUND",
                    $"probe target '{probeTarget}' is not an existing directory",
                    "pass an existing directory you own");
                return (int)CliExitCode.InvalidArgument;
            }

            report = report with
            {
                Components = [.. report.Components, .. FilesystemProbe.Run(probeTarget, cancellationToken)],
            };
        }

        if (context.Format == OutputFormat.Json)
        {
            // AC-003: the JSON document IS the capability report, unwrapped.
            MachineWriter.WriteDocument(context, report);
        }
        else if (context.Format == OutputFormat.Jsonl)
        {
            foreach (var component in report.Components)
            {
                MachineWriter.WriteEventLine(context, MachineWriter.NewEvent(
                    context,
                    "doctor",
                    "doctor.component",
                    component.Available ? "succeeded" : "unsupported",
                    component.Available ? null : [NotImplemented],
                    pathToken: component.Id));
            }

            MachineWriter.WriteEventLine(context, MachineWriter.NewEvent(
                context, "doctor", "doctor.completed", "succeeded"));
        }
        else
        {
            RenderHuman(context, report);
        }

        return (int)CliExitCode.Success;
    }

    private static void RenderHuman(OutputContext context, CapabilityReport report)
    {
        context.Out.WriteLine($"{CliHost.ExecutableName} doctor — diagnostic mode; no files are read or modified");
        if (context.Quiet)
        {
            return;
        }

        foreach (var entry in report.Platform)
        {
            context.Out.WriteLine($"  platform.{entry.Key}: {entry.Value}");
        }

        foreach (var component in report.Components)
        {
            context.Out.WriteLine($"  {component.Id}: {(component.Available ? "available" : component.Status)}");
        }

        foreach (var format in report.Formats)
        {
            var abilities = new List<string>();
            if (format.Read)
            {
                abilities.Add("read");
            }

            if (format.MetadataRead)
            {
                abilities.Add("metadata");
            }

            context.Out.WriteLine(
                $"  format.{format.Format}: {(abilities.Count > 0 ? string.Join('+', abilities) : "gated")}"
                + $" [{string.Join(',', format.ReasonCodes ?? [])}]");
        }
    }

    private static CapabilityReport BuildReport(
        OrientExpress.Application.Metadata.IMetadataService metadataService,
        OrientExpress.Application.Inference.IModelRegistry? modelRegistry)
    {
        var exifToolIdentity = metadataService.Identify();
        var metadataAvailable = exifToolIdentity.IsSuccess;
        var activeModel = modelRegistry?.GetActive();
        var activeInstalled = activeModel is { IsSuccess: true } && activeModel.Value.Value is not null;
        return new CapabilityReport
        {
            GeneratedUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Tool = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["name"] = CliHost.ExecutableName,
                ["version"] = VersionCommand.ToolVersion,
                ["contract"] = CliHost.ContractVersion,
            },
            Platform = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["os"] = RuntimeInformation.OSDescription,
                ["framework"] = RuntimeInformation.FrameworkDescription,
                ["architecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["longPathsEnabled"] = ProbeLongPaths(),
                ["tempWritable"] = ProbeTempWritable(),
            },
            Components =
            [
                new ComponentCapability { Id = "cli-host", Available = true, Status = "implemented (WP-002)", Version = VersionCommand.ToolVersion },
                new ComponentCapability { Id = "configuration", Available = true, Status = "implemented (WP-003)" },
                new ComponentCapability { Id = "filesystem-identity", Available = true, Status = "implemented (WP-004); guarded execution gated (WP-012/WP-015)" },
                new ComponentCapability
                {
                    Id = "metadata-exiftool",
                    Available = metadataAvailable,
                    Status = metadataAvailable
                        ? "implemented (WP-005); sha256 " + exifToolIdentity.Require().Sha256[..16]
                        : "gated: EXIFTOOL_NOT_FOUND (install ExifTool; see DEP-007)",
                    Version = metadataAvailable ? exifToolIdentity.Require().Version : null,
                },
                new ComponentCapability { Id = "format-prober", Available = true, Status = "implemented (WP-005); header probe, no pixel decode" },
                new ComponentCapability { Id = "inference-runtime", Available = true, Status = "implemented (WP-006); onnxruntime cpu (ADR-0004 baseline)" },
                new ComponentCapability
                {
                    Id = "orientation-model",
                    Available = activeInstalled,
                    Status = activeInstalled
                        ? $"active: {activeModel!.Value.Value!.Manifest.ModelId} {activeModel.Value.Value.Manifest.Version} "
                            + $"(sha256 {activeModel.Value.Value.Manifest.Artifact!.Sha256![..16]}…); decision engine gated (WP-007/008)"
                        : "no active model; install and activate one via 'upright models'",
                    Version = activeInstalled ? activeModel!.Value.Value!.Manifest.Version : null,
                },
                new ComponentCapability { Id = "job-store", Available = false, Status = "gated: WP-009 (SPIKE-008 evidence recorded)" },
            ],
            Formats =
            [
                BaselineFormat("jpeg", metadataAvailable),
                BaselineFormat("png", metadataAvailable),
                GatedFormat("tiff", "NOT_YET_IMPLEMENTED"),
                GatedFormat("heic", "CODEC_PRESENT_UNVERIFIED_OR_ABSENT"),
                GatedFormat("avif", "CODEC_PRESENT_UNVERIFIED_OR_ABSENT"),
            ],
        };
    }

    private static FormatCapability BaselineFormat(string format, bool metadataAvailable)
        => new()
        {
            Format = format,
            Read = true,
            MetadataRead = metadataAvailable,
            Transform = false,
            MetadataWrite = metadataAvailable,
            Verify = false,
            ReasonCodes = metadataAvailable
                ? ["TRANSFORM_GATED_WP-010", "VERIFY_GATED_WP-013"]
                : ["EXIFTOOL_NOT_FOUND", "TRANSFORM_GATED_WP-010", "VERIFY_GATED_WP-013"],
        };

    private static FormatCapability GatedFormat(string format, string reason)
        => new()
        {
            Format = format,
            Read = false,
            MetadataRead = false,
            Transform = false,
            MetadataWrite = false,
            Verify = false,
            ReasonCodes = [reason],
        };

    private static string ProbeLongPaths()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "unknown";
        }

        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\FileSystem");
            return key?.GetValue("LongPathsEnabled") is 1 ? "true" : "false";
        }
        catch (System.Security.SecurityException)
        {
            return "unknown";
        }
    }

    private static string ProbeTempWritable()
    {
        try
        {
            var probePath = Path.Combine(Path.GetTempPath(), "upright-doctor-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probePath, "probe");
            File.Delete(probePath);
            return "true";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "false";
        }
    }
}
