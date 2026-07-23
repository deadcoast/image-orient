using Microsoft.Data.Sqlite;
using OrientExpress.Spike008;

namespace OrientExpress.Spike008.Tests;

/// <summary>
/// Encodes the DOC-DATAMODEL state-transition constraints as executable rules:
/// append-only events, sealed-plan immutability, forward-only journal
/// checkpoints, guarded migrations, and seal tamper detection.
/// </summary>
public sealed class JobStoreTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("spike008-tests-").FullName;

    private string NewDbPath() => Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".db");

    private static IReadOnlyList<SyntheticRow> Rows(int count)
        => SyntheticRows.Generate(count);

    [Fact]
    public void Create_configures_wal_normal_and_schema_v1()
    {
        using var store = JobStore.Create(NewDbPath(), "job-1", "safe-copy");
        Assert.Equal("wal", store.JournalMode);
        Assert.Equal(1, store.SchemaVersion);
        Assert.Equal("ok", store.IntegrityCheck());
    }

    [Fact]
    public void Events_are_append_only()
    {
        using var store = JobStore.Create(NewDbPath(), "job-1", "safe-copy");
        store.AppendEvent("scan.file", "succeeded", fileId: null);

        Assert.Throws<SqliteException>(() => store.ExecuteForTest("UPDATE events SET result = 'failed'"));
        Assert.Throws<SqliteException>(() => store.ExecuteForTest("DELETE FROM events"));
        Assert.Equal(1, store.CountEvents());
    }

    [Fact]
    public void Plan_rows_are_mutable_before_seal_and_immutable_after()
    {
        using var store = JobStore.Create(NewDbPath(), "job-1", "safe-copy");
        store.InsertFilesBatch(Rows(10), batchSize: 5);

        store.ExecuteForTest("UPDATE plans SET action = 'KEEP' WHERE file_id = 1");

        var seal = store.SealPlan();
        Assert.NotEmpty(seal);
        Assert.Throws<SqliteException>(() => store.ExecuteForTest("UPDATE plans SET action = 'KEEP' WHERE file_id = 2"));
        Assert.Throws<SqliteException>(() => store.ExecuteForTest("DELETE FROM plans WHERE file_id = 2"));
        Assert.Throws<SqliteException>(() => store.ExecuteForTest(
            "INSERT INTO plans(file_id, action, output_token, row_digest) VALUES (999, 'KEEP', 'x', 'y')"));
    }

    [Fact]
    public void Seal_survives_reopen_and_detects_tampering()
    {
        var dbPath = NewDbPath();
        string seal;
        using (var store = JobStore.Create(dbPath, "job-1", "safe-copy"))
        {
            store.InsertFilesBatch(Rows(50), batchSize: 25);
            seal = store.SealPlan();
        }

        using (var reopened = JobStore.Open(dbPath))
        {
            Assert.True(reopened.VerifySeal());
            reopened.ExecuteForTest("DROP TRIGGER plans_sealed_no_update");
            reopened.ExecuteForTest("UPDATE plans SET action = 'ROTATE_180' WHERE file_id = 3");
            Assert.False(reopened.VerifySeal());
        }
    }

    [Fact]
    public void Journal_checkpoints_advance_forward_only()
    {
        using var store = JobStore.Create(NewDbPath(), "job-1", "safe-copy");
        store.InsertFilesBatch(Rows(1), batchSize: 1);
        store.OpenJournalIntent("intent-1", fileId: 1);

        store.AdvanceJournal("intent-1", "StagedVerified");
        store.AdvanceJournal("intent-1", "IntentDurable");
        Assert.Throws<SqliteException>(() => store.AdvanceJournal("intent-1", "Prepared"));
        Assert.Throws<SqliteException>(() => store.AdvanceJournal("intent-1", "IntentDurable"));
        store.AdvanceJournal("intent-1", "Committed");
    }

    [Fact]
    public void Migration_to_v2_is_recorded_and_guarded()
    {
        var dbPath = NewDbPath();
        using var store = JobStore.Create(dbPath, "job-1", "safe-copy");
        store.InsertFilesBatch(Rows(20), batchSize: 10);

        store.MigrateToV2();
        Assert.Equal(2, store.SchemaVersion);
        Assert.Equal(1, store.CountMigrations());
        Assert.Throws<InvalidOperationException>(store.MigrateToV2);
        Assert.Equal("ok", store.IntegrityCheck());
    }

    [Fact]
    public void Future_schema_versions_are_refused_not_guessed()
    {
        var dbPath = NewDbPath();
        using (var store = JobStore.Create(dbPath, "job-1", "safe-copy"))
        {
            store.ExecuteForTest("PRAGMA user_version = 99");
        }

        Assert.Throws<InvalidOperationException>(() => JobStore.Open(dbPath));
    }

    [Fact]
    public void Batched_inserts_land_exactly_and_counts_match()
    {
        using var store = JobStore.Create(NewDbPath(), "job-1", "safe-copy");
        store.InsertFilesBatch(Rows(1234), batchSize: 100);

        Assert.Equal(1234, store.CountFiles());
        var counts = store.ActionCounts();
        Assert.Equal(1234, counts.Values.Sum());
        Assert.True(counts.ContainsKey("KEEP"));
    }

    [Fact]
    public void Pending_batch_query_returns_actionable_rows_in_order()
    {
        using var store = JobStore.Create(NewDbPath(), "job-1", "safe-copy");
        store.InsertFilesBatch(Rows(100), batchSize: 50);

        var pending = store.NextActionableBatch(10);
        Assert.Equal(10, pending.Count);
        Assert.Equal(pending.OrderBy(id => id), pending);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }
}
