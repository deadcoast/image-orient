using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace OrientExpress.Spike008;

public sealed record SyntheticRow(long Seq, string PathToken, long Size, string WeakFingerprint, string Action, string OutputToken);

/// <summary>Deterministic synthetic corpus rows: no user data, reproducible from the sequence number.</summary>
public static class SyntheticRows
{
    private static readonly string[] Actions =
        ["KEEP", "KEEP", "KEEP", "KEEP", "KEEP", "KEEP", "KEEP", "KEEP", "KEEP", "KEEP",
         "KEEP", "KEEP", "KEEP", "KEEP", "ROTATE_90_CW", "ROTATE_90_CW", "ROTATE_180", "ROTATE_270_CW", "REVIEW", "UNSUPPORTED"];

    public static IReadOnlyList<SyntheticRow> Generate(int count, long startSeq = 1)
    {
        var rows = new List<SyntheticRow>(count);
        for (var index = 0; index < count; index++)
        {
            var seq = startSeq + index;
            var action = Actions[seq % Actions.Length];
            rows.Add(new SyntheticRow(
                Seq: seq,
                PathToken: string.Create(CultureInfo.InvariantCulture, $"img-{seq:D8}.jpg"),
                Size: 1_000_000 + (seq % 4_000_000),
                WeakFingerprint: string.Create(CultureInfo.InvariantCulture, $"wfp-{seq:x16}"),
                Action: action,
                OutputToken: string.Create(CultureInfo.InvariantCulture, $"out/img-{seq:D8}.jpg")));
        }

        return rows;
    }
}

/// <summary>
/// SPIKE-008 prototype of the DOC-DATAMODEL job store: WAL + NORMAL sync,
/// single logical writer, append-only events, sealed immutable plans,
/// forward-only journal checkpoints, and guarded forward-only migrations.
/// </summary>
public sealed class JobStore : IDisposable
{
    public const int SupportedSchemaVersion = 2;
    private readonly SqliteConnection _connection;

    private JobStore(SqliteConnection connection, string jobId)
    {
        _connection = connection;
        JobId = jobId;
    }

    public string JobId { get; }

    public int SchemaVersion => (int)ScalarLong("PRAGMA user_version");

    public string JournalMode => (string)Scalar("PRAGMA journal_mode")!;

    public static JobStore Create(string dbPath, string jobId, string mode)
    {
        var connection = OpenConnection(dbPath);
        Execute(connection, SchemaV1Sql);
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO jobs(job_id, contract_version, created_utc, mode, state) VALUES ($id, $contract, $utc, $mode, 'created')";
            command.Parameters.AddWithValue("$id", jobId);
            command.Parameters.AddWithValue("$contract", "job-store/1.0-draft");
            command.Parameters.AddWithValue("$utc", NowUtc());
            command.Parameters.AddWithValue("$mode", mode);
            command.ExecuteNonQuery();
        }

        return new JobStore(connection, jobId);
    }

    public static JobStore Open(string dbPath)
    {
        if (!File.Exists(dbPath))
        {
            throw new InvalidOperationException($"No job database at '{dbPath}'.");
        }

        var connection = OpenConnection(dbPath);
        var version = (long)(Scalar(connection, "PRAGMA user_version") ?? 0L);
        if (version is < 1 or > SupportedSchemaVersion)
        {
            connection.Dispose();
            // DOC-DATAMODEL: a newer unsupported version is diagnosed, never guessed.
            throw new InvalidOperationException(
                $"Job schema version {version} is outside the supported range 1..{SupportedSchemaVersion}.");
        }

        var jobId = (string)(Scalar(connection, "SELECT job_id FROM jobs LIMIT 1")
            ?? throw new InvalidOperationException("Job database has no jobs row."));
        return new JobStore(connection, jobId);
    }

    public void InsertFilesBatch(IReadOnlyList<SyntheticRow> rows, int batchSize)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        using var fileCommand = _connection.CreateCommand();
        fileCommand.CommandText =
            "INSERT INTO files(job_id, seq, path_token, size, weak_fp) VALUES ($job, $seq, $path, $size, $wfp)";
        var jobParameter = fileCommand.Parameters.AddWithValue("$job", JobId);
        var seqParameter = fileCommand.Parameters.Add("$seq", SqliteType.Integer);
        var pathParameter = fileCommand.Parameters.Add("$path", SqliteType.Text);
        var sizeParameter = fileCommand.Parameters.Add("$size", SqliteType.Integer);
        var fingerprintParameter = fileCommand.Parameters.Add("$wfp", SqliteType.Text);

        using var planCommand = _connection.CreateCommand();
        planCommand.CommandText =
            "INSERT INTO plans(file_id, action, output_token, row_digest) VALUES (last_insert_rowid(), $action, $output, $digest)";
        var actionParameter = planCommand.Parameters.Add("$action", SqliteType.Text);
        var outputParameter = planCommand.Parameters.Add("$output", SqliteType.Text);
        var digestParameter = planCommand.Parameters.Add("$digest", SqliteType.Text);

        for (var offset = 0; offset < rows.Count; offset += batchSize)
        {
            using var transaction = _connection.BeginTransaction();
            fileCommand.Transaction = transaction;
            planCommand.Transaction = transaction;
            foreach (var row in rows.Skip(offset).Take(batchSize))
            {
                seqParameter.Value = row.Seq;
                pathParameter.Value = row.PathToken;
                sizeParameter.Value = row.Size;
                fingerprintParameter.Value = row.WeakFingerprint;
                fileCommand.ExecuteNonQuery();

                actionParameter.Value = row.Action;
                outputParameter.Value = row.OutputToken;
                digestParameter.Value = RowDigest(row);
                planCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
    }

    /// <summary>Canonical ordered serialization digested incrementally (scales to 1m rows).</summary>
    public string ComputePlanDigest()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT file_id, action, output_token, row_digest FROM plans ORDER BY file_id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"{reader.GetInt64(0)}|{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetString(3)}\n");
            hash.AppendData(Encoding.UTF8.GetBytes(line));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public string SealPlan()
    {
        var digest = ComputePlanDigest();
        using var command = _connection.CreateCommand();
        command.CommandText = "UPDATE jobs SET plan_seal = $seal, state = 'sealed' WHERE job_id = $job";
        command.Parameters.AddWithValue("$seal", digest);
        command.Parameters.AddWithValue("$job", JobId);
        command.ExecuteNonQuery();
        return digest;
    }

    public bool VerifySeal()
    {
        var sealed_ = Scalar("SELECT plan_seal FROM jobs LIMIT 1") as string;
        return sealed_ is not null && sealed_ == ComputePlanDigest();
    }

    public void AppendEvent(string eventType, string result, long? fileId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO events(job_id, file_id, event_type, result, at_utc) VALUES ($job, $file, $type, $result, $utc)";
        command.Parameters.AddWithValue("$job", JobId);
        command.Parameters.AddWithValue("$file", (object?)fileId ?? DBNull.Value);
        command.Parameters.AddWithValue("$type", eventType);
        command.Parameters.AddWithValue("$result", result);
        command.Parameters.AddWithValue("$utc", NowUtc());
        command.ExecuteNonQuery();
    }

    public void AppendEventsBatch(int count, string eventType)
    {
        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO events(job_id, file_id, event_type, result, at_utc) VALUES ($job, $file, $type, 'succeeded', $utc)";
        command.Parameters.AddWithValue("$job", JobId);
        var fileParameter = command.Parameters.Add("$file", SqliteType.Integer);
        command.Parameters.AddWithValue("$type", eventType);
        command.Parameters.AddWithValue("$utc", NowUtc());
        for (var fileId = 1L; fileId <= count; fileId++)
        {
            fileParameter.Value = fileId;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public void OpenJournalIntent(string intentId, long fileId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO journal(intent_id, file_id, checkpoint, updated_utc) VALUES ($id, $file, 'Prepared', $utc)";
        command.Parameters.AddWithValue("$id", intentId);
        command.Parameters.AddWithValue("$file", fileId);
        command.Parameters.AddWithValue("$utc", NowUtc());
        command.ExecuteNonQuery();
    }

    public void AdvanceJournal(string intentId, string checkpoint)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            "UPDATE journal SET checkpoint = $checkpoint, updated_utc = $utc WHERE intent_id = $id";
        command.Parameters.AddWithValue("$checkpoint", checkpoint);
        command.Parameters.AddWithValue("$utc", NowUtc());
        command.Parameters.AddWithValue("$id", intentId);
        command.ExecuteNonQuery();
    }

    public long CountFiles() => ScalarLong("SELECT COUNT(*) FROM files");

    public long CountEvents() => ScalarLong("SELECT COUNT(*) FROM events");

    public long CountMigrations() => ScalarLong("SELECT COUNT(*) FROM schema_migrations");

    public IReadOnlyDictionary<string, long> ActionCounts()
    {
        var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT action, COUNT(*) FROM plans GROUP BY action";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            counts[reader.GetString(0)] = reader.GetInt64(1);
        }

        return counts;
    }

    public IReadOnlyList<long> NextActionableBatch(int limit)
    {
        var ids = new List<long>(limit);
        using var command = _connection.CreateCommand();
        command.CommandText =
            "SELECT file_id FROM plans WHERE action LIKE 'ROTATE_%' ORDER BY file_id LIMIT $limit";
        command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    public string IntegrityCheck() => (string)Scalar("PRAGMA integrity_check")!;

    public void Checkpoint() => Execute(_connection, "PRAGMA wal_checkpoint(TRUNCATE)");

    public void MigrateToV2()
    {
        if (SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Migration to v2 requires schema v1; database is at v{SchemaVersion}.");
        }

        using var transaction = _connection.BeginTransaction();
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE files ADD COLUMN attributes INTEGER NOT NULL DEFAULT 0;
            CREATE INDEX idx_files_size ON files(size);
            INSERT INTO schema_migrations(from_version, to_version, applied_utc, status)
                VALUES (1, 2, $utc, 'applied');
            """;
        command.Parameters.AddWithValue("$utc", NowUtc());
        command.ExecuteNonQuery();
        transaction.Commit();
        Execute(_connection, "PRAGMA user_version = 2");
    }

    /// <summary>Raw SQL escape hatch for tests and the crash harness only.</summary>
    public void ExecuteForTest(string sql) => Execute(_connection, sql);

    public string SqliteVersion() => (string)Scalar("SELECT sqlite_version()")!;

    public void Dispose() => _connection.Dispose();

    private static SqliteConnection OpenConnection(string dbPath)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());
        connection.Open();
        Execute(connection, "PRAGMA journal_mode = WAL");
        Execute(connection, "PRAGMA synchronous = NORMAL");
        Execute(connection, "PRAGMA foreign_keys = ON");
        Execute(connection, "PRAGMA busy_timeout = 5000");
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql) => Scalar(_connection, sql);

    private long ScalarLong(string sql) => (long)(Scalar(_connection, sql) ?? 0L);

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static string RowDigest(SyntheticRow row)
        => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(row.PathToken + "|" + row.Action + "|" + row.OutputToken)))[..32];

    private static string NowUtc()
        => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    private const string SchemaV1Sql = """
        CREATE TABLE jobs(
            job_id TEXT PRIMARY KEY,
            contract_version TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            mode TEXT NOT NULL,
            state TEXT NOT NULL,
            plan_seal TEXT);
        CREATE TABLE files(
            file_id INTEGER PRIMARY KEY,
            job_id TEXT NOT NULL REFERENCES jobs(job_id),
            seq INTEGER NOT NULL,
            path_token TEXT NOT NULL,
            size INTEGER NOT NULL,
            weak_fp TEXT NOT NULL,
            UNIQUE(job_id, seq));
        CREATE TABLE plans(
            file_id INTEGER PRIMARY KEY REFERENCES files(file_id),
            action TEXT NOT NULL,
            output_token TEXT NOT NULL,
            row_digest TEXT NOT NULL);
        CREATE INDEX idx_plans_action ON plans(action);
        CREATE TABLE events(
            event_seq INTEGER PRIMARY KEY AUTOINCREMENT,
            job_id TEXT NOT NULL,
            file_id INTEGER,
            event_type TEXT NOT NULL,
            result TEXT NOT NULL,
            at_utc TEXT NOT NULL);
        CREATE TABLE journal(
            intent_id TEXT PRIMARY KEY,
            file_id INTEGER NOT NULL,
            checkpoint TEXT NOT NULL,
            updated_utc TEXT NOT NULL);
        CREATE TABLE schema_migrations(
            migration_id INTEGER PRIMARY KEY AUTOINCREMENT,
            from_version INTEGER NOT NULL,
            to_version INTEGER NOT NULL,
            applied_utc TEXT NOT NULL,
            status TEXT NOT NULL);
        CREATE TRIGGER events_no_update BEFORE UPDATE ON events
            BEGIN SELECT RAISE(ABORT, 'events are append-only'); END;
        CREATE TRIGGER events_no_delete BEFORE DELETE ON events
            BEGIN SELECT RAISE(ABORT, 'events are append-only'); END;
        CREATE TRIGGER plans_sealed_no_update BEFORE UPDATE ON plans
            WHEN (SELECT plan_seal FROM jobs LIMIT 1) IS NOT NULL
            BEGIN SELECT RAISE(ABORT, 'plan is sealed'); END;
        CREATE TRIGGER plans_sealed_no_delete BEFORE DELETE ON plans
            WHEN (SELECT plan_seal FROM jobs LIMIT 1) IS NOT NULL
            BEGIN SELECT RAISE(ABORT, 'plan is sealed'); END;
        CREATE TRIGGER plans_sealed_no_insert BEFORE INSERT ON plans
            WHEN (SELECT plan_seal FROM jobs LIMIT 1) IS NOT NULL
            BEGIN SELECT RAISE(ABORT, 'plan is sealed'); END;
        CREATE TRIGGER journal_forward_only BEFORE UPDATE OF checkpoint ON journal
            WHEN (CASE NEW.checkpoint
                    WHEN 'Prepared' THEN 1 WHEN 'StagedVerified' THEN 2 WHEN 'IntentDurable' THEN 3
                    WHEN 'Replaced' THEN 4 WHEN 'Committed' THEN 5 WHEN 'Reconciled' THEN 6 ELSE 0 END)
                 <= (CASE OLD.checkpoint
                    WHEN 'Prepared' THEN 1 WHEN 'StagedVerified' THEN 2 WHEN 'IntentDurable' THEN 3
                    WHEN 'Replaced' THEN 4 WHEN 'Committed' THEN 5 WHEN 'Reconciled' THEN 6 ELSE 0 END)
            BEGIN SELECT RAISE(ABORT, 'journal checkpoint must advance'); END;
        PRAGMA user_version = 1;
        """;
}
