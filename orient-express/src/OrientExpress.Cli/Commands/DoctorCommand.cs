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

    public static int Run(OutputContext context, string? probeTarget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var report = BuildReport();
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

        context.Out.WriteLine(
            "  formats: none enabled yet (codec, metadata, and transform evidence pending; see STATUS.md)");
    }

    private static CapabilityReport BuildReport()
    {
        string[] gatedFormats = ["jpeg", "png", "heic", "avif", "tiff"];
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
                new ComponentCapability { Id = "metadata-exiftool", Available = false, Status = "gated: SPIKE-004 + WP-005" },
                new ComponentCapability { Id = "codecs", Available = false, Status = "gated: SPIKE-007 + WP-005" },
                new ComponentCapability { Id = "inference-runtime", Available = false, Status = "gated: SPIKE-003 + WP-006" },
                new ComponentCapability { Id = "orientation-model", Available = false, Status = "gated: SPIKE-001/002 corpus and proof" },
                new ComponentCapability { Id = "job-store", Available = false, Status = "gated: WP-009 (SPIKE-008 evidence recorded)" },
            ],
            Formats = [.. gatedFormats.Select(format => new FormatCapability
            {
                Format = format,
                Read = false,
                MetadataRead = false,
                Transform = false,
                MetadataWrite = false,
                Verify = false,
                ReasonCodes = [NotImplemented],
            })],
        };
    }

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
