using System.Text.Json;
using OrientExpress.Cli.Hosting;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Jobs;
using OrientExpress.Contracts.Transform;
using OrientExpress.Infrastructure.Sqlite;

namespace OrientExpress.CliTests;

/// <summary>
/// `upright jobs …` golden tests (PRD-FR-028, AC-049/AC-050) on a hermetic
/// job root seeded through the store — no external tools needed.
/// </summary>
public sealed class JobsCommandsTests : IAsyncLifetime
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-jobs-cli-tests-").FullName;

    private string JobRoot => Path.Combine(_directory, "data", "jobs");

    public async ValueTask InitializeAsync()
    {
        await using var store = new SqliteJobStore(JobRoot);
        var manifest = new JobManifest
        {
            JobIdValue = "job-a",
            CreatedUtc = "2026-07-24T00:00:00.0000000+00:00",
            ToolVersion = "0.1.0-test",
            Mode = "safe-copy",
            SourceRoot = Path.Combine(_directory, "photos"),
            OutputRoot = Path.Combine(_directory, "photos-upright"),
            ConfigDigest = new string('a', 64),
        };
        _ = (await store.CreateAsync(manifest, CancellationToken.None)).Require();
        _ = (await store.UpsertScanBatchAsync(
            new JobId("job-a"),
            [
                new PlannedFile
                {
                    Sequence = 1,
                    SourcePath = Path.Combine(_directory, "photos", "p.jpg"),
                    SizeBytes = 100,
                    LastWriteUtc = "2026-07-24T00:00:00.0000000+00:00",
                    PrefixSha256 = new string('b', 64),
                    VolumeSerialHex = "0",
                    FileIdHex = "1",
                    Format = "jpeg",
                    Width = 96,
                    Height = 64,
                    ExifOrientation = 6,
                    Action = PlanAction.Normalize,
                    Operation = JpegOperation.Rotate90,
                    EdgePolicy = JpegEdgePolicy.Abort,
                    OutputToken = "p.jpg",
                    ExpectedWidth = 64,
                    ExpectedHeight = 96,
                    ExpectedOrientation = 1,
                },
            ],
            CancellationToken.None)).Require();
        _ = (await store.SealPlanAsync(new JobId("job-a"), CancellationToken.None)).Require();
    }

    public ValueTask DisposeAsync()
    {
        Directory.Delete(_directory, recursive: true);
        return ValueTask.CompletedTask;
    }

    private (int ExitCode, string Stdout, string Stderr) Invoke(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var environment = new HostEnvironment(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Path.Combine(_directory, "user", "config.json"),
            Path.Combine(_directory, "data"));
        var exitCode = CliHost.Invoke(args, stdout, stderr, hostEnvironment: environment);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void List_and_show_report_the_seeded_job()
    {
        var list = Invoke("jobs", "list", "--json");
        Assert.Equal(0, list.ExitCode);
        using (var document = JsonDocument.Parse(list.Stdout))
        {
            var job = document.RootElement.GetProperty("jobs").EnumerateArray().Single();
            Assert.Equal("job-a", job.GetProperty("jobId").GetString());
            Assert.Equal("safe-copy", job.GetProperty("mode").GetString());
        }

        var show = Invoke("jobs", "show", "job-a", "--json");
        Assert.Equal(0, show.ExitCode);
        using (var document = JsonDocument.Parse(show.Stdout))
        {
            Assert.True(document.RootElement.GetProperty("sealed_").GetBoolean());
            Assert.Equal(
                1, document.RootElement.GetProperty("planCounts").GetProperty("Normalize").GetInt64());
            Assert.Equal(0, document.RootElement.GetProperty("committedRows").GetInt64());
        }

        Assert.Equal(
            (int)CliExitCode.InvalidArgument, Invoke("jobs", "show", "no-such-job").ExitCode);
    }

    [Fact]
    public void Integrity_reports_ok_then_detects_a_missing_artifact()
    {
        var ok = Invoke("jobs", "integrity", "job-a", "--json");
        Assert.Equal(0, ok.ExitCode);

        // AC-049: a missing on-disk artifact is an integrity finding.
        File.Delete(Path.Combine(JobRoot, "job-a", "plan.digest"));
        var broken = Invoke("jobs", "integrity", "--json");
        Assert.Equal((int)CliExitCode.VerificationFailed, broken.ExitCode);
        Assert.Contains("PLAN_DIGEST_FILE_MISSING", broken.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Prune_is_dry_run_by_default_and_removes_with_yes()
    {
        var dry = Invoke("jobs", "prune", "job-a");
        Assert.Equal(0, dry.ExitCode);
        Assert.Contains("dry-run", dry.Stdout, StringComparison.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(JobRoot, "job-a")));

        var real = Invoke("jobs", "prune", "job-a", "--yes");
        Assert.Equal(0, real.ExitCode);
        Assert.False(Directory.Exists(Path.Combine(JobRoot, "job-a")));

        Assert.Equal((int)CliExitCode.InvalidArgument, Invoke("jobs", "prune", "job-a", "--yes").ExitCode);
    }

    [Fact]
    public void Verify_with_nothing_committed_is_nothing_to_do()
        => Assert.Equal((int)CliExitCode.NothingToDo, Invoke("verify", "job-a").ExitCode);
}
