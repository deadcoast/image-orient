using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace OrientExpress.Spike008;

public sealed record ScenarioResult(
    string Id,
    string Description,
    string Outcome,
    IReadOnlyDictionary<string, string> Measurements,
    string? Error);

public sealed class ScenarioRecorder
{
    private readonly SortedDictionary<string, string> _measurements = new(StringComparer.Ordinal);
    private bool _expectationsHeld = true;

    public void Measure(string key, string value) => _measurements[key] = value;

    public void Measure(string key, long value) => Measure(key, value.ToString(CultureInfo.InvariantCulture));

    public void Measure(string key, bool value) => Measure(key, value ? "true" : "false");

    public void Expect(string key, bool held)
    {
        Measure(key, held);
        _expectationsHeld &= held;
    }

    public ScenarioResult Complete(string id, string description)
        => new(id, description, _expectationsHeld ? "pass" : "fail", _measurements, Error: null);

    public ScenarioResult Failed(string id, string description, Exception exception)
        => new(id, description, "fail", _measurements, exception.ToString());
}

/// <summary>
/// SPIKE-008 evidence: durability configuration, batching economics, crash
/// recovery, and the ADR-0008 gate at 100k and 1m synthetic rows with
/// falsifiable targets (declared in the report before the run).
/// </summary>
public static class EvidenceRunner
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        var resultsPath = "spike008-results.json";
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            if (args[index] == "--results")
            {
                resultsPath = args[index + 1];
            }
        }

        var workRoot = Path.Combine(
            Path.GetTempPath(),
            "spike008-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(workRoot);

        var results = new List<ScenarioResult>
        {
            RunSafely(
                "E-CONFIG", "WAL + NORMAL sync + integrity are active",
                recorder => ConfigProbe(recorder, workRoot)),
            RunSafely(
                "E-BATCHING", "transaction batching economics: single-row vs 1k-row transactions",
                recorder => Batching(recorder, workRoot)),
            RunSafely(
                "E-CRASH-MID-TXN", "kernel kill mid-transaction: only committed rows survive; integrity ok",
                recorder => Crash(recorder, workRoot, "mid-transaction")),
            RunSafely(
                "E-CRASH-AFTER-COMMIT", "kernel kill after commit: every committed row is durable",
                recorder => Crash(recorder, workRoot, "after-commit")),
            RunSafely(
                "E-SCALE-100K", "ADR-0008 gate at 100k synthetic rows",
                recorder => Scale(recorder, workRoot, 100_000)),
            RunSafely(
                "E-SCALE-1M", "ADR-0008 gate at 1m synthetic rows",
                recorder => Scale(recorder, workRoot, 1_000_000)),
        };

        WriteReport(results, resultsPath, workRoot);
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Outcome,-5} {result.Id}");
        }

        var failed = results.Count(result => result.Outcome == "fail");
        Console.WriteLine($"spike008: total={results.Count} pass={results.Count - failed} fail={failed}");
        Console.WriteLine($"spike008: results written to {Path.GetFullPath(resultsPath)}");
        return failed == 0 ? 0 : 1;
    }

    private static ScenarioResult RunSafely(string id, string description, Action<ScenarioRecorder> body)
    {
        var recorder = new ScenarioRecorder();
        try
        {
            body(recorder);
            return recorder.Complete(id, description);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or SqliteException)
        {
            return recorder.Failed(id, description, exception);
        }
    }

    private static void ConfigProbe(ScenarioRecorder recorder, string workRoot)
    {
        using var store = JobStore.Create(Path.Combine(workRoot, "config.db"), "job-config", "safe-copy");
        recorder.Expect("journal_mode_wal", store.JournalMode == "wal");
        recorder.Measure("sqlite_version", store.SqliteVersion());
        recorder.Expect("integrity_ok", store.IntegrityCheck() == "ok");
    }

    private static void Batching(ScenarioRecorder recorder, string workRoot)
    {
        const int SingleRows = 2_000;
        const int BatchedRows = 50_000;

        using (var singles = JobStore.Create(Path.Combine(workRoot, "singles.db"), "job-singles", "safe-copy"))
        {
            var stopwatch = Stopwatch.StartNew();
            singles.InsertFilesBatch(SyntheticRows.Generate(SingleRows), batchSize: 1);
            stopwatch.Stop();
            recorder.Measure("single_row_txn_rows_per_sec", (long)(SingleRows / stopwatch.Elapsed.TotalSeconds));
        }

        using (var batched = JobStore.Create(Path.Combine(workRoot, "batched.db"), "job-batched", "safe-copy"))
        {
            var stopwatch = Stopwatch.StartNew();
            batched.InsertFilesBatch(SyntheticRows.Generate(BatchedRows), batchSize: 1_000);
            stopwatch.Stop();
            var rate = (long)(BatchedRows / stopwatch.Elapsed.TotalSeconds);
            recorder.Measure("batched_1k_rows_per_sec", rate);
            // Target declared in the report: batched sustained insert >= 20k rows/sec.
            recorder.Expect("batched_meets_20k_per_sec_target", rate >= 20_000);
        }
    }

    private static void Crash(ScenarioRecorder recorder, string workRoot, string fault)
    {
        var dbPath = Path.Combine(workRoot, "crash-" + fault + ".db");
        const int CommittedBatches = 20;
        const int BatchSize = 500;

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath
                ?? throw new InvalidOperationException("Environment.ProcessPath is unavailable."),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[]
        {
            "crash-writer", "--db", dbPath,
            "--batches", CommittedBatches.ToString(CultureInfo.InvariantCulture),
            "--batch-size", BatchSize.ToString(CultureInfo.InvariantCulture),
            "--fault", fault,
        })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var child = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start crash-writer child.");
        if (!child.WaitForExit(120_000))
        {
            child.Kill(entireProcessTree: true);
            throw new InvalidOperationException("crash-writer child timed out.");
        }

        recorder.Measure("child_exit_code", child.ExitCode);
        recorder.Expect("child_crashed_as_injected", child.ExitCode == 42);

        using var reopened = JobStore.Open(dbPath);
        recorder.Expect("integrity_ok_after_crash", reopened.IntegrityCheck() == "ok");
        var files = reopened.CountFiles();
        recorder.Measure("rows_after_recovery", files);
        recorder.Expect("exactly_committed_rows_survive", files == CommittedBatches * BatchSize);
    }

    private static void Scale(ScenarioRecorder recorder, string workRoot, int rows)
    {
        var dbPath = Path.Combine(
            workRoot, string.Create(CultureInfo.InvariantCulture, $"scale-{rows}.db"));

        string sealDigest;
        using (var store = JobStore.Create(dbPath, "job-scale", "safe-copy"))
        {
            var insert = Stopwatch.StartNew();
            const int Chunk = 100_000;
            for (var start = 0; start < rows; start += Chunk)
            {
                store.InsertFilesBatch(
                    SyntheticRows.Generate(Math.Min(Chunk, rows - start), startSeq: start + 1),
                    batchSize: 5_000);
            }

            insert.Stop();
            var insertRate = (long)(rows / insert.Elapsed.TotalSeconds);
            recorder.Measure("insert_rows_per_sec", insertRate);
            recorder.Expect("insert_meets_20k_per_sec_target", insertRate >= 20_000);

            var events = Stopwatch.StartNew();
            var eventCount = Math.Min(rows, 100_000);
            store.AppendEventsBatch(eventCount, "scan.file");
            events.Stop();
            recorder.Measure("event_append_rows_per_sec", (long)(eventCount / events.Elapsed.TotalSeconds));

            var sealTimer = Stopwatch.StartNew();
            sealDigest = store.SealPlan();
            sealTimer.Stop();
            recorder.Measure("seal_ms", sealTimer.ElapsedMilliseconds);

            var checkpoint = Stopwatch.StartNew();
            store.Checkpoint();
            checkpoint.Stop();
            recorder.Measure("wal_checkpoint_ms", checkpoint.ElapsedMilliseconds);
        }

        recorder.Measure("db_size_bytes", new FileInfo(dbPath).Length);

        var restart = Stopwatch.StartNew();
        using (var reopened = JobStore.Open(dbPath))
        {
            var counts = reopened.ActionCounts();
            restart.Stop();
            recorder.Measure("restart_open_and_counts_ms", restart.ElapsedMilliseconds);
            recorder.Expect("restart_counts_under_2s_target", restart.ElapsedMilliseconds <= 2_000);
            recorder.Expect("counts_total_matches", counts.Values.Sum() == rows);

            var pending = Stopwatch.StartNew();
            var batch = reopened.NextActionableBatch(500);
            pending.Stop();
            recorder.Measure("pending_batch_query_ms", pending.ElapsedMilliseconds);
            recorder.Expect("pending_batch_returns_rows", batch.Count == 500);

            var verify = Stopwatch.StartNew();
            recorder.Expect("seal_verifies_after_restart", reopened.VerifySeal());
            verify.Stop();
            recorder.Measure("seal_verify_ms", verify.ElapsedMilliseconds);
            recorder.Measure("plan_seal_prefix", sealDigest[..16]);

            var migrate = Stopwatch.StartNew();
            reopened.MigrateToV2();
            migrate.Stop();
            recorder.Measure("migration_v1_to_v2_ms", migrate.ElapsedMilliseconds);
            recorder.Expect("migration_under_30s_target", migrate.ElapsedMilliseconds <= 30_000);

            var integrity = Stopwatch.StartNew();
            recorder.Expect("integrity_ok", reopened.IntegrityCheck() == "ok");
            integrity.Stop();
            recorder.Measure("integrity_check_ms", integrity.ElapsedMilliseconds);
        }

        File.Delete(dbPath);
    }

    private static void WriteReport(List<ScenarioResult> results, string resultsPath, string workRoot)
    {
        var report = new
        {
            spike = "SPIKE-008",
            environment = new
            {
                osDescription = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                dependency = "Microsoft.Data.Sqlite 10.0.10 (DEP-011)",
                harnessVersion = "0.1.0",
                workRoot,
                runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            },
            scenarios = results,
            summary = new
            {
                total = results.Count,
                pass = results.Count(result => result.Outcome == "pass"),
                fail = results.Count(result => result.Outcome == "fail"),
            },
        };
        var directory = Path.GetDirectoryName(Path.GetFullPath(resultsPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resultsPath, JsonSerializer.Serialize(report, ReportOptions) + "\n");
    }
}
