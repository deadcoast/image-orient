using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using OrientExpress.Spike006.Native;
using OrientExpress.Spike006.Scenarios;

namespace OrientExpress.Spike006.Reporting;

public sealed record EnvironmentManifest(
    string OsDescription,
    string OsVersion,
    string Framework,
    string ProcessArchitecture,
    string VolumeFileSystem,
    string VolumeSerialHex,
    string HarnessVersion,
    string FixturePolicy,
    string RunUtc);

public sealed record RunSummary(int Total, int Pass, int Fail, int Skipped, int SourceLossCases);

public sealed record SpikeRunReport(
    string Spike,
    EnvironmentManifest Environment,
    IReadOnlyList<ScenarioResult> Scenarios,
    RunSummary Summary);

public static class RunReportWriter
{
    public const string HarnessVersion = "0.1.0";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static SpikeRunReport Build(string workRoot, IReadOnlyList<ScenarioResult> scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        return new SpikeRunReport(
            Spike: "SPIKE-006",
            Environment: ProbeEnvironment(workRoot),
            Scenarios: scenarios,
            Summary: new RunSummary(
                Total: scenarios.Count,
                Pass: scenarios.Count(s => s.Outcome == ScenarioResult.Pass),
                Fail: scenarios.Count(s => s.Outcome == ScenarioResult.Fail),
                Skipped: scenarios.Count(s => s.Outcome == ScenarioResult.Skipped),
                SourceLossCases: scenarios.Count(s => !s.SourceSafetyHeld)));
    }

    public static void Write(SpikeRunReport report, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(report, SerializerOptions) + "\n");
    }

    private static EnvironmentManifest ProbeEnvironment(string workRoot)
    {
        var fileSystem = "unknown";
        var serialHex = "unknown";
        using (var handle = NativeMethods.CreateFile(
            workRoot,
            0,
            NativeMethods.ShareReadWriteDelete,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FlagBackupSemantics,
            0))
        {
            if (!handle.IsInvalid
                && NativeMethods.TryGetVolumeInformation(handle, out var probedFileSystem, out var serial, out _))
            {
                fileSystem = probedFileSystem;
                serialHex = serial.ToString("x8", CultureInfo.InvariantCulture);
            }
        }

        return new EnvironmentManifest(
            OsDescription: RuntimeInformation.OSDescription,
            OsVersion: System.Environment.OSVersion.VersionString,
            Framework: RuntimeInformation.FrameworkDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            VolumeFileSystem: fileSystem,
            VolumeSerialHex: serialHex,
            HarnessVersion: HarnessVersion,
            FixturePolicy: "synthetic deterministic fixtures derived from scenario ids; no user photos (SPIKE-001 consent rules)",
            RunUtc: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }
}
