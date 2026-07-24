using Microsoft.Data.Sqlite;
using OrientExpress.Application.Jobs;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Jobs;
using OrientExpress.Contracts.Transform;
using OrientExpress.Infrastructure.Sqlite;

namespace OrientExpress.InfrastructureTests;

/// <summary>
/// PRD-FR-007/008/014/028 store rules on hermetic temp job roots: immutable
/// manifests, batched scan writes, write-once content-addressed seals with
/// schema-trigger immutability (AC-012/AC-013), append-only events, stale
/// input drift detection (AC-014), and diagnosed — never guessed — schema
/// versions.
/// </summary>
public sealed class SqliteJobStoreTests : IAsyncLifetime
{
    private readonly string _root = Directory.CreateTempSubdirectory("upright-jobstore-tests-").FullName;
    private SqliteJobStore _store = null!;

    public ValueTask InitializeAsync()
    {
        _store = new SqliteJobStore(Path.Combine(_root, "jobs"));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DisposeAsync();
        Directory.Delete(_root, recursive: true);
    }

    private static JobManifest Manifest(string jobId) => new()
    {
        JobIdValue = jobId,
        CreatedUtc = DateTimeOffset.UtcNow.ToString("O"),
        ToolVersion = "0.1.0-test",
        Mode = "safe-copy",
        SourceRoot = @"C:\photos",
        OutputRoot = @"C:\photos-upright",
        ConfigDigest = new string('a', 64),
    };

    private static PlannedFile Row(long sequence, PlanAction action = PlanAction.Normalize) => new()
    {
        Sequence = sequence,
        SourcePath = $@"C:\photos\img-{sequence:D6}.jpg",
        SizeBytes = 1000 + sequence,
        LastWriteUtc = "2026-07-24T00:00:00.0000000+00:00",
        PrefixSha256 = new string('b', 64),
        VolumeSerialHex = "cafe",
        FileIdHex = $"{sequence:x8}",
        Format = "jpeg",
        Width = 640,
        Height = 480,
        ExifOrientation = 6,
        Action = action,
        Operation = action == PlanAction.Normalize ? JpegOperation.Rotate90 : JpegOperation.None,
        EdgePolicy = JpegEdgePolicy.Abort,
        OutputToken = $"out/img-{sequence:D6}.jpg",
        ExpectedWidth = 480,
        ExpectedHeight = 640,
        ExpectedOrientation = 1,
    };

    [Fact]
    public async Task Create_writes_manifest_and_opens_with_wal_schema_v1()
    {
        var handle = (await _store.CreateAsync(Manifest("job-a"), CancellationToken.None)).Require();
        Assert.True(File.Exists(Path.Combine(handle.JobDirectory, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(handle.JobDirectory, "job.db")));

        var reopened = (await _store.OpenAsync(new JobId("job-a"), CancellationToken.None)).Require();
        Assert.Equal("job-store/1.0", reopened.Manifest.ContractVersion);
        Assert.Equal("safe-copy", reopened.Manifest.Mode);
        Assert.Null(reopened.Manifest.ModelIdentity);

        var duplicate = await _store.CreateAsync(Manifest("job-a"), CancellationToken.None);
        Assert.Equal("JOB_ALREADY_EXISTS", duplicate.Error!.Code);
    }

    [Fact]
    public async Task Scan_batch_seal_and_read_round_trip_preserves_every_field()
    {
        var job = new JobId("job-rt");
        _ = (await _store.CreateAsync(Manifest(job.Value), CancellationToken.None)).Require();
        var rows = Enumerable.Range(1, 50).Select(i => Row(i, i % 5 == 0 ? PlanAction.Review : PlanAction.Normalize)).ToList();
        Assert.Equal(50, (await _store.UpsertScanBatchAsync(job, rows, CancellationToken.None)).Require());

        var seal = (await _store.SealPlanAsync(job, CancellationToken.None)).Require();
        Assert.Equal(50, seal.RowCount);
        Assert.Equal(64, seal.Sha256.Length);
        Assert.Contains(seal.Sha256, File.ReadAllText(Path.Combine(_root, "jobs", job.Value, "plan.digest")));

        var verified = (await _store.VerifySealAsync(job, CancellationToken.None)).Require();
        Assert.Equal(seal.Sha256, verified!.Sha256);

        var reviews = new List<PlannedFile>();
        await foreach (var row in _store.ReadPlanAsync(job, new PlanQuery(PlanAction.Review), CancellationToken.None))
        {
            reviews.Add(row);
        }

        Assert.Equal(10, reviews.Count);
        Assert.Equal(reviews.OrderBy(r => r.Sequence).Select(r => r.Sequence), reviews.Select(r => r.Sequence));
        var full = new List<PlannedFile>();
        await foreach (var row in _store.ReadPlanAsync(job, new PlanQuery(), CancellationToken.None))
        {
            full.Add(row);
        }

        Assert.Equal(rows, full);
    }

    [Fact]
    public async Task Sealed_plan_refuses_mutation_and_seal_is_write_once()
    {
        var job = new JobId("job-seal");
        _ = (await _store.CreateAsync(Manifest(job.Value), CancellationToken.None)).Require();
        _ = (await _store.UpsertScanBatchAsync(job, [Row(1)], CancellationToken.None)).Require();
        _ = (await _store.SealPlanAsync(job, CancellationToken.None)).Require();

        // AC-013: the schema trigger — not politeness — refuses the write.
        var mutation = await _store.UpsertScanBatchAsync(job, [Row(2)], CancellationToken.None);
        Assert.Equal("PLAN_SEALED", mutation.Error!.Code);

        var reseal = await _store.SealPlanAsync(job, CancellationToken.None);
        Assert.Equal("PLAN_ALREADY_SEALED", reseal.Error!.Code);
    }

    [Fact]
    public async Task Events_are_append_only_and_tampering_breaks_the_seal()
    {
        var job = new JobId("job-events");
        _ = (await _store.CreateAsync(Manifest(job.Value), CancellationToken.None)).Require();
        _ = (await _store.UpsertScanBatchAsync(job, [Row(1)], CancellationToken.None)).Require();
        _ = (await _store.AppendEventsAsync(
            job,
            [new JobEvent("scan", "succeeded", 1), new JobEvent("seal", "succeeded")],
            CancellationToken.None)).Require();
        _ = (await _store.SealPlanAsync(job, CancellationToken.None)).Require();

        var integrity = (await _store.CheckIntegrityAsync(job, CancellationToken.None)).Require();
        Assert.True(integrity.DatabaseOk);
        Assert.True(integrity.SealMatches);
        Assert.Equal(1, integrity.FileCount);
        Assert.Equal(2, integrity.EventCount);
        Assert.Empty(integrity.Problems);

        // Tamper directly (dropping the trigger first, as an attacker with
        // file access could): integrity + seal verification must catch it.
        await _store.DisposeAsync();
        using (var connection = new SqliteConnection(
            $"Data Source={Path.Combine(_root, "jobs", job.Value, "job.db")};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                DROP TRIGGER plan_rows_sealed_no_update;
                UPDATE plan_rows SET output_token = 'evil' WHERE seq = 1;
                """;
            command.ExecuteNonQuery();
        }

        _store = new SqliteJobStore(Path.Combine(_root, "jobs"));
        var verify = await _store.VerifySealAsync(job, CancellationToken.None);
        Assert.Equal("PLAN_SEAL_MISMATCH", verify.Error!.Code);
        var report = (await _store.CheckIntegrityAsync(job, CancellationToken.None)).Require();
        Assert.Contains("PLAN_SEAL_MISMATCH", report.Problems);
    }

    [Fact]
    public async Task Unknown_job_and_future_schema_version_are_typed_errors()
    {
        var missing = await _store.OpenAsync(new JobId("nope"), CancellationToken.None);
        Assert.Equal("JOB_NOT_FOUND", missing.Error!.Code);

        var job = new JobId("job-future");
        _ = (await _store.CreateAsync(Manifest(job.Value), CancellationToken.None)).Require();
        await _store.DisposeAsync();
        using (var connection = new SqliteConnection(
            $"Data Source={Path.Combine(_root, "jobs", job.Value, "job.db")};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99";
            command.ExecuteNonQuery();
        }

        _store = new SqliteJobStore(Path.Combine(_root, "jobs"));
        var future = await _store.OpenAsync(job, CancellationToken.None);
        Assert.Equal("JOB_SCHEMA_UNSUPPORTED", future.Error!.Code);
    }

    [Fact]
    public async Task List_returns_manifests_in_stable_order()
    {
        _ = (await _store.CreateAsync(Manifest("job-1"), CancellationToken.None)).Require();
        _ = (await _store.CreateAsync(Manifest("job-2"), CancellationToken.None)).Require();
        var jobs = (await _store.ListAsync(CancellationToken.None)).Require();
        Assert.Equal(["job-1", "job-2"], jobs.Select(j => j.Id.Value));
    }

    [Fact]
    public void Stale_input_check_names_every_drift_and_passes_identical_state()
    {
        var planned = Row(1);
        var matching = Snapshot(planned.SizeBytes, planned.LastWriteUtc, planned.PrefixSha256, "cafe", "00000001");
        Assert.Empty(StaleInputCheck.FindDrift(planned, matching));

        var drifted = Snapshot(planned.SizeBytes + 1, "2026-07-25T00:00:00.0000000+00:00", new string('c', 64), "beef", "ff");
        var reasons = StaleInputCheck.FindDrift(planned, drifted);
        Assert.Equal(
            ["SIZE_CHANGED", "LAST_WRITE_CHANGED", "CONTENT_PREFIX_CHANGED", "VOLUME_CHANGED", "FILE_ID_CHANGED"],
            reasons);

        Assert.True(StaleInputCheck.SatisfiesStrength(planned, FingerprintStrength.Standard));
        Assert.False(StaleInputCheck.SatisfiesStrength(planned, FingerprintStrength.Strong));
        Assert.True(StaleInputCheck.SatisfiesStrength(
            planned with { FullSha256 = new string('d', 64) }, FingerprintStrength.Strong));
    }

    private static FileSnapshot Snapshot(long size, string lastWrite, string prefix, string volume, string fileId)
        => new(
            new NormalizedPath(@"C:\photos\img-000001.jpg", @"\\?\C:\photos\img-000001.jpg"),
            VolumeSerialHex: volume,
            FileIdHex: fileId,
            LinkCount: 1,
            IsReparsePoint: false,
            IsCloudPlaceholder: false,
            IsOffline: false,
            new FileFingerprint(FingerprintStrength.Standard, size, lastWrite, prefix, null),
            new FilesystemCapability(FilesystemCapabilityClass.SupportedNtfs, "NTFS", volume, "Fixed"));
}
