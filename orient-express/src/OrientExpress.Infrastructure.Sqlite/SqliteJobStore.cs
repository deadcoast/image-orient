using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using OrientExpress.Application.Jobs;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Jobs;
using OrientExpress.Contracts.Transform;

namespace OrientExpress.Infrastructure.Sqlite;

/// <summary>
/// COMP-JOBSTORE on SQLite with the SPIKE-008-proven settings (WAL +
/// synchronous NORMAL + batched single-writer transactions + schema
/// triggers as the constraint of record). One directory per job under the
/// configured job root: `manifest.json` (immutable), `job.db` (canonical),
/// `plan.digest` (seal echo for humans; the database column is
/// authoritative). Append-only events and sealed-plan immutability are
/// enforced IN the schema, so no code path — including future ones — can
/// mutate history silently.
/// </summary>
public sealed class SqliteJobStore(string jobRoot) : IJobStore
{
    public const int SupportedSchemaVersion = 1;
    private const string ContractVersion = "job-store/1.0";

    private static readonly JsonSerializerOptions ManifestJson = new() { WriteIndented = true };

    private readonly Dictionary<string, SqliteConnection> _connections = [];
    private readonly SemaphoreSlim _writer = new(1, 1);

    public async ValueTask<Result<JobHandle>> CreateAsync(JobManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var jobDirectory = Path.Combine(jobRoot, manifest.JobIdValue);
        if (Directory.Exists(jobDirectory))
        {
            return Fail<JobHandle>("JOB_ALREADY_EXISTS", ErrorCategory.InvalidInput,
                $"A job directory for '{manifest.JobIdValue}' already exists.");
        }

        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(jobDirectory);
            Directory.CreateDirectory(Path.Combine(jobDirectory, "reports"));
            Directory.CreateDirectory(Path.Combine(jobDirectory, "diagnostics"));

            var connection = OpenConnection(Path.Combine(jobDirectory, "job.db"));
            Execute(connection, SchemaV1Sql);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO jobs(job_id, contract_version, created_utc, tool_version, mode,
                                     source_root, output_root, config_digest, model_identity, policy_identity, state)
                    VALUES ($id, $contract, $created, $tool, $mode, $root, $output, $config, $model, $policy, 'created')
                    """;
                command.Parameters.AddWithValue("$id", manifest.JobIdValue);
                command.Parameters.AddWithValue("$contract", ContractVersion);
                command.Parameters.AddWithValue("$created", manifest.CreatedUtc);
                command.Parameters.AddWithValue("$tool", manifest.ToolVersion);
                command.Parameters.AddWithValue("$mode", manifest.Mode);
                command.Parameters.AddWithValue("$root", manifest.SourceRoot);
                command.Parameters.AddWithValue("$output", manifest.OutputRoot);
                command.Parameters.AddWithValue("$config", manifest.ConfigDigest);
                command.Parameters.AddWithValue("$model", (object?)manifest.ModelIdentity ?? DBNull.Value);
                command.Parameters.AddWithValue("$policy", (object?)manifest.PolicyIdentity ?? DBNull.Value);
                command.ExecuteNonQuery();
            }

            // The manifest file is written once and never rewritten.
            var manifestPath = Path.Combine(jobDirectory, "manifest.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(manifest, ManifestJson) + "\n",
                cancellationToken).ConfigureAwait(false);

            _connections[manifest.JobIdValue] = connection;
            return Result.Ok(new JobHandle(new JobId(manifest.JobIdValue), manifest, jobDirectory));
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<JobHandle>> OpenAsync(JobId job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<JobHandle>(opened.Error!);
            }

            var manifestPath = Path.Combine(jobRoot, job.Value, "manifest.json");
            var manifest = JsonSerializer.Deserialize<JobManifest>(
                await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
            return manifest is null
                ? Fail<JobHandle>("JOB_MANIFEST_INVALID", ErrorCategory.IntegrityViolation,
                    "manifest.json did not deserialize to a job manifest.")
                : Result.Ok(new JobHandle(job, manifest, Path.Combine(jobRoot, job.Value)));
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return Fail<JobHandle>("JOB_MANIFEST_INVALID", ErrorCategory.IntegrityViolation, exception.Message);
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<long>> AppendEventsAsync(
        JobId job, IReadOnlyList<JobEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(events);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<long>(opened.Error!);
            }

            var connection = opened.Require();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO events(job_id, file_seq, event_type, result, reason_code, data_json, at_utc)
                VALUES ($job, $file, $type, $result, $reason, $data, $utc)
                """;
            command.Parameters.AddWithValue("$job", job.Value);
            var fileParameter = command.Parameters.Add("$file", SqliteType.Integer);
            var typeParameter = command.Parameters.Add("$type", SqliteType.Text);
            var resultParameter = command.Parameters.Add("$result", SqliteType.Text);
            var reasonParameter = command.Parameters.Add("$reason", SqliteType.Text);
            var dataParameter = command.Parameters.Add("$data", SqliteType.Text);
            command.Parameters.AddWithValue("$utc", NowUtc());
            foreach (var jobEvent in events)
            {
                fileParameter.Value = (object?)jobEvent.FileSequence ?? DBNull.Value;
                typeParameter.Value = jobEvent.EventType;
                resultParameter.Value = jobEvent.Result;
                reasonParameter.Value = (object?)jobEvent.ReasonCode ?? DBNull.Value;
                dataParameter.Value = (object?)jobEvent.DataJson ?? DBNull.Value;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
            return Result.Ok((long)events.Count);
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<long>> UpsertScanBatchAsync(
        JobId job, IReadOnlyList<PlannedFile> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(files);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<long>(opened.Error!);
            }

            var connection = opened.Require();
            using var transaction = connection.BeginTransaction();
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR REPLACE INTO plan_rows(
                    job_id, seq, source_path, size_bytes, last_write_utc, prefix_sha256, full_sha256,
                    volume_serial, file_id_hex, format, width, height, exif_orientation,
                    action, operation, edge_policy, output_token,
                    expected_width, expected_height, expected_orientation, reason_code, row_digest)
                VALUES ($job, $seq, $path, $size, $lastWrite, $prefix, $full,
                        $volume, $fileId, $format, $width, $height, $exif,
                        $action, $operation, $edge, $output,
                        $expWidth, $expHeight, $expOrientation, $reason, $digest)
                """;
            command.Parameters.AddWithValue("$job", job.Value);
            var parameters = new Dictionary<string, SqliteParameter>();
            foreach (var name in new[]
            {
                "$seq", "$path", "$size", "$lastWrite", "$prefix", "$full", "$volume", "$fileId",
                "$format", "$width", "$height", "$exif", "$action", "$operation", "$edge",
                "$output", "$expWidth", "$expHeight", "$expOrientation", "$reason", "$digest",
            })
            {
                parameters[name] = command.Parameters.Add(name, SqliteType.Text);
            }

            try
            {
                foreach (var file in files)
                {
                    parameters["$seq"].Value = file.Sequence;
                    parameters["$path"].Value = file.SourcePath;
                    parameters["$size"].Value = file.SizeBytes;
                    parameters["$lastWrite"].Value = file.LastWriteUtc;
                    parameters["$prefix"].Value = file.PrefixSha256;
                    parameters["$full"].Value = (object?)file.FullSha256 ?? DBNull.Value;
                    parameters["$volume"].Value = file.VolumeSerialHex;
                    parameters["$fileId"].Value = file.FileIdHex;
                    parameters["$format"].Value = file.Format;
                    parameters["$width"].Value = file.Width;
                    parameters["$height"].Value = file.Height;
                    parameters["$exif"].Value = file.ExifOrientation;
                    parameters["$action"].Value = file.Action.ToString();
                    parameters["$operation"].Value = file.Operation.ToString();
                    parameters["$edge"].Value = file.EdgePolicy.ToString();
                    parameters["$output"].Value = file.OutputToken;
                    parameters["$expWidth"].Value = file.ExpectedWidth;
                    parameters["$expHeight"].Value = file.ExpectedHeight;
                    parameters["$expOrientation"].Value = file.ExpectedOrientation;
                    parameters["$reason"].Value = (object?)file.ReasonCode ?? DBNull.Value;
                    parameters["$digest"].Value = RowDigest(file);
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (SqliteException exception) when (exception.Message.Contains("plan is sealed", StringComparison.Ordinal))
            {
                return Fail<long>("PLAN_SEALED", ErrorCategory.InvalidInput,
                    "The plan is sealed; planned rows are immutable (AC-013).");
            }

            return Result.Ok((long)files.Count);
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<PlanSeal>> SealPlanAsync(JobId job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<PlanSeal>(opened.Error!);
            }

            var connection = opened.Require();
            if (Scalar(connection, "SELECT plan_seal FROM jobs LIMIT 1") is string)
            {
                return Fail<PlanSeal>("PLAN_ALREADY_SEALED", ErrorCategory.InvalidInput,
                    "The plan is already sealed; seals are written once.");
            }

            var (digest, rowCount) = ComputePlanDigest(connection);
            var sealedUtc = NowUtc();
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "UPDATE jobs SET plan_seal = $seal, sealed_utc = $utc, state = 'sealed' WHERE job_id = $job";
                command.Parameters.AddWithValue("$seal", digest);
                command.Parameters.AddWithValue("$utc", sealedUtc);
                command.Parameters.AddWithValue("$job", job.Value);
                command.ExecuteNonQuery();
            }

            // Human-readable echo; job.db stays authoritative.
            await File.WriteAllTextAsync(
                Path.Combine(jobRoot, job.Value, "plan.digest"),
                $"sha256:{digest} rows:{rowCount} sealed:{sealedUtc}\n",
                cancellationToken).ConfigureAwait(false);
            Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
            return Result.Ok(new PlanSeal(digest, rowCount, sealedUtc));
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<PlanSeal?>> VerifySealAsync(JobId job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<PlanSeal?>(opened.Error!);
            }

            var connection = opened.Require();
            if (Scalar(connection, "SELECT plan_seal FROM jobs LIMIT 1") is not string recorded)
            {
                return Result.Ok<PlanSeal?>(null);
            }

            var (digest, rowCount) = ComputePlanDigest(connection);
            var sealedUtc = (string)(Scalar(connection, "SELECT sealed_utc FROM jobs LIMIT 1") ?? "");
            return digest == recorded
                ? Result.Ok<PlanSeal?>(new PlanSeal(digest, rowCount, sealedUtc))
                : Fail<PlanSeal?>("PLAN_SEAL_MISMATCH", ErrorCategory.IntegrityViolation,
                    "The plan rows no longer match the recorded seal digest.");
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async IAsyncEnumerable<PlannedFile> ReadPlanAsync(
        JobId job, PlanQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(query);
        var opened = Connection(job);
        if (!opened.IsSuccess)
        {
            yield break;
        }

        using var command = opened.Require().CreateCommand();
        command.CommandText = PlanRowSelectSql.Replace(
            "ORDER BY seq",
            "WHERE seq > $after AND ($action IS NULL OR action = $action) ORDER BY seq LIMIT $limit",
            StringComparison.Ordinal);
        command.Parameters.AddWithValue("$after", query.AfterSequence);
        command.Parameters.AddWithValue("$action", (object?)query.Action?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$limit", query.Limit);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return MapRow(reader);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private const string PlanRowSelectSql = """
        SELECT seq, source_path, size_bytes, last_write_utc, prefix_sha256, full_sha256,
               volume_serial, file_id_hex, format, width, height, exif_orientation,
               action, operation, edge_policy, output_token,
               expected_width, expected_height, expected_orientation, reason_code
        FROM plan_rows
        ORDER BY seq
        """;

    private static PlannedFile MapRow(SqliteDataReader reader) => new()
    {
        Sequence = reader.GetInt64(0),
        SourcePath = reader.GetString(1),
        SizeBytes = reader.GetInt64(2),
        LastWriteUtc = reader.GetString(3),
        PrefixSha256 = reader.GetString(4),
        FullSha256 = reader.IsDBNull(5) ? null : reader.GetString(5),
        VolumeSerialHex = reader.GetString(6),
        FileIdHex = reader.GetString(7),
        Format = reader.GetString(8),
        Width = reader.GetInt32(9),
        Height = reader.GetInt32(10),
        ExifOrientation = reader.GetInt32(11),
        Action = Enum.Parse<PlanAction>(reader.GetString(12)),
        Operation = Enum.Parse<JpegOperation>(reader.GetString(13)),
        EdgePolicy = Enum.Parse<JpegEdgePolicy>(reader.GetString(14)),
        OutputToken = reader.GetString(15),
        ExpectedWidth = reader.GetInt32(16),
        ExpectedHeight = reader.GetInt32(17),
        ExpectedOrientation = reader.GetInt32(18),
        ReasonCode = reader.IsDBNull(19) ? null : reader.GetString(19),
    };

    /// <summary>Sequences that already carry the given event type — the resume ledger (AC-028).</summary>
    public async ValueTask<Result<IReadOnlySet<long>>> ReadEventSequencesAsync(
        JobId job, string eventType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<IReadOnlySet<long>>(opened.Error!);
            }

            var sequences = new HashSet<long>();
            using var command = opened.Require().CreateCommand();
            command.CommandText =
                "SELECT DISTINCT file_seq FROM events WHERE event_type = $type AND file_seq IS NOT NULL";
            command.Parameters.AddWithValue("$type", eventType);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                _ = sequences.Add(reader.GetInt64(0));
            }

            return Result.Ok<IReadOnlySet<long>>(sequences);
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<IntegrityReport>> CheckIntegrityAsync(JobId job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<IntegrityReport>(opened.Error!);
            }

            var connection = opened.Require();
            var problems = new List<string>();
            var databaseOk = (string)(Scalar(connection, "PRAGMA integrity_check") ?? "") == "ok";
            if (!databaseOk)
            {
                problems.Add("SQLITE_INTEGRITY_CHECK_FAILED");
            }

            var sealPresent = Scalar(connection, "SELECT plan_seal FROM jobs LIMIT 1") is string;
            var sealMatches = false;
            if (sealPresent)
            {
                var verify = ComputePlanDigest(connection);
                sealMatches = verify.Digest == (string)Scalar(connection, "SELECT plan_seal FROM jobs LIMIT 1")!;
                if (!sealMatches)
                {
                    problems.Add("PLAN_SEAL_MISMATCH");
                }
            }

            var unresolvedIntents = ScalarLong(
                connection, "SELECT COUNT(*) FROM journal WHERE checkpoint NOT IN ('Committed', 'Reconciled')");
            if (unresolvedIntents > 0)
            {
                problems.Add("UNRESOLVED_JOURNAL_INTENTS");
            }

            // AC-049: missing on-disk artifacts are integrity findings too.
            var jobDirectory = Path.Combine(jobRoot, job.Value);
            if (!File.Exists(Path.Combine(jobDirectory, "manifest.json")))
            {
                problems.Add("MANIFEST_MISSING");
            }

            if (sealPresent && !File.Exists(Path.Combine(jobDirectory, "plan.digest")))
            {
                problems.Add("PLAN_DIGEST_FILE_MISSING");
            }

            return Result.Ok(new IntegrityReport(
                databaseOk,
                sealPresent,
                sealMatches,
                ScalarLong(connection, "SELECT COUNT(*) FROM plan_rows"),
                ScalarLong(connection, "SELECT COUNT(*) FROM events"),
                unresolvedIntents,
                problems));
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<IReadOnlyList<JobHandle>>> ListAsync(CancellationToken cancellationToken)
    {
        var handles = new List<JobHandle>();
        if (!Directory.Exists(jobRoot))
        {
            return Result.Ok<IReadOnlyList<JobHandle>>(handles);
        }

        foreach (var directory in Directory.EnumerateDirectories(jobRoot).Order(StringComparer.Ordinal))
        {
            var manifestPath = Path.Combine(directory, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<JobManifest>(
                    await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false));
                if (manifest is not null)
                {
                    handles.Add(new JobHandle(new JobId(manifest.JobIdValue), manifest, directory));
                }
            }
            catch (JsonException)
            {
                // A corrupt manifest is a diagnosable job, not a crash; jobs
                // integrity (WP-017) reports it.
            }
        }

        return Result.Ok<IReadOnlyList<JobHandle>>(handles);
    }

    public async ValueTask<Result<bool>> OpenIntentAsync(
        JobId job, JournalIntent intent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(intent);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<bool>(opened.Error!);
            }

            using var command = opened.Require().CreateCommand();
            command.CommandText = """
                INSERT INTO journal(intent_id, file_seq, operation, checkpoint, details_json, updated_utc)
                VALUES ($id, $seq, $operation, $checkpoint, $details, $utc)
                """;
            command.Parameters.AddWithValue("$id", intent.IntentId);
            command.Parameters.AddWithValue("$seq", intent.FileSequence);
            command.Parameters.AddWithValue("$operation", intent.Operation);
            command.Parameters.AddWithValue("$checkpoint", JournalCheckpoints.Prepared);
            command.Parameters.AddWithValue("$details", (object?)intent.DetailsJson ?? DBNull.Value);
            command.Parameters.AddWithValue("$utc", NowUtc());
            _ = command.ExecuteNonQuery();
            return Result.Ok(true);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            return Fail<bool>("JOURNAL_INTENT_DUPLICATE", ErrorCategory.InvalidInput,
                $"intent '{intent.IntentId}' already exists");
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<bool>> AdvanceIntentAsync(
        JobId job, string intentId, string checkpoint, string? detailsJson, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<bool>(opened.Error!);
            }

            using var command = opened.Require().CreateCommand();
            command.CommandText = detailsJson is null
                ? "UPDATE journal SET checkpoint = $checkpoint, updated_utc = $utc WHERE intent_id = $id"
                : "UPDATE journal SET checkpoint = $checkpoint, details_json = $details, updated_utc = $utc WHERE intent_id = $id";
            command.Parameters.AddWithValue("$checkpoint", checkpoint);
            command.Parameters.AddWithValue("$utc", NowUtc());
            command.Parameters.AddWithValue("$id", intentId);
            if (detailsJson is not null)
            {
                command.Parameters.AddWithValue("$details", detailsJson);
            }

            return command.ExecuteNonQuery() == 1
                ? Result.Ok(true)
                : Fail<bool>("JOURNAL_INTENT_NOT_FOUND", ErrorCategory.NotFound,
                    $"no journal intent '{intentId}'");
        }
        catch (SqliteException exception) when (
            exception.Message.Contains("journal checkpoint must advance", StringComparison.Ordinal))
        {
            return Fail<bool>("JOURNAL_CHECKPOINT_REGRESSION", ErrorCategory.InvalidInput,
                $"checkpoint '{checkpoint}' does not advance intent '{intentId}'");
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<IReadOnlyList<JournalIntent>>> ReadIntentsAsync(
        JobId job, bool unresolvedOnly, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var opened = Connection(job);
            if (!opened.IsSuccess)
            {
                return Result.Fail<IReadOnlyList<JournalIntent>>(opened.Error!);
            }

            var intents = new List<JournalIntent>();
            using var command = opened.Require().CreateCommand();
            command.CommandText = unresolvedOnly
                ? "SELECT intent_id, file_seq, operation, checkpoint, details_json, updated_utc FROM journal "
                    + "WHERE checkpoint NOT IN ('Committed', 'Reconciled') ORDER BY intent_id"
                : "SELECT intent_id, file_seq, operation, checkpoint, details_json, updated_utc FROM journal "
                    + "ORDER BY intent_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                intents.Add(new JournalIntent(
                    reader.GetString(0),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.GetString(5)));
            }

            return Result.Ok<IReadOnlyList<JournalIntent>>(intents);
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public async ValueTask<Result<string>> PruneAsync(JobId job, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        await _writer.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var jobDirectory = Path.Combine(jobRoot, job.Value);
            if (!Directory.Exists(jobDirectory))
            {
                return Fail<string>("JOB_NOT_FOUND", ErrorCategory.NotFound,
                    $"No job directory for '{job.Value}' under the job root.");
            }

            // AC-050: a job with pending recovery work cannot be removed.
            var opened = Connection(job);
            if (opened.IsSuccess)
            {
                var unresolved = ScalarLong(
                    opened.Require(),
                    "SELECT COUNT(*) FROM journal WHERE checkpoint NOT IN ('Committed', 'Reconciled')");
                if (unresolved > 0)
                {
                    return Fail<string>("JOB_RECOVERY_PENDING", ErrorCategory.InvalidInput,
                        $"{unresolved} journal intent(s) are unresolved; recover before pruning.");
                }
            }

            if (dryRun)
            {
                return Result.Ok($"dry-run: would remove '{jobDirectory}'");
            }

            if (_connections.Remove(job.Value, out var connection))
            {
                connection.Dispose();
            }

            Directory.Delete(jobDirectory, recursive: true);
            return Result.Ok($"removed '{jobDirectory}'");
        }
        finally
        {
            _ = _writer.Release();
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var connection in _connections.Values)
        {
            connection.Dispose();
        }

        _connections.Clear();
        _writer.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Time-ordered filesystem-safe job id.</summary>
    public static string NewJobId()
        => DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
            + "-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    private Result<SqliteConnection> Connection(JobId job)
    {
        if (_connections.TryGetValue(job.Value, out var cached))
        {
            return Result.Ok(cached);
        }

        var dbPath = Path.Combine(jobRoot, job.Value, "job.db");
        if (!File.Exists(dbPath))
        {
            return Fail<SqliteConnection>("JOB_NOT_FOUND", ErrorCategory.NotFound,
                $"No job database for '{job.Value}' under the job root.");
        }

        var connection = OpenConnection(dbPath);
        var version = (long)(Scalar(connection, "PRAGMA user_version") ?? 0L);
        if (version is < 1 or > SupportedSchemaVersion)
        {
            connection.Dispose();
            // DOC-DATAMODEL: a newer unsupported version is diagnosed, never guessed.
            return Fail<SqliteConnection>("JOB_SCHEMA_UNSUPPORTED", ErrorCategory.CapabilityUnavailable,
                $"Job schema version {version} is outside the supported range 1..{SupportedSchemaVersion}.");
        }

        _connections[job.Value] = connection;
        return Result.Ok(connection);
    }

    /// <summary>
    /// The seal digests actual row CONTENT, recomputed on every read — never
    /// the stored row_digest column, which an attacker with file access
    /// could leave stale while editing the row it describes.
    /// </summary>
    private static (string Digest, long RowCount) ComputePlanDigest(SqliteConnection connection)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var rowCount = 0L;
        using var command = connection.CreateCommand();
        command.CommandText = PlanRowSelectSql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var row = MapRow(reader);
            hash.AppendData(Encoding.UTF8.GetBytes(
                string.Create(CultureInfo.InvariantCulture, $"{row.Sequence}|{RowDigest(row)}\n")));
            rowCount++;
        }

        return (Convert.ToHexStringLower(hash.GetHashAndReset()), rowCount);
    }

    /// <summary>Canonical per-row digest: every sealed field, order-stable, culture-invariant.</summary>
    public static string RowDigest(PlannedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var canonical = string.Join('|',
            file.Sequence.ToString(CultureInfo.InvariantCulture),
            file.SourcePath,
            file.SizeBytes.ToString(CultureInfo.InvariantCulture),
            file.LastWriteUtc,
            file.PrefixSha256,
            file.FullSha256 ?? "",
            file.VolumeSerialHex,
            file.FileIdHex,
            file.Format,
            file.Width.ToString(CultureInfo.InvariantCulture),
            file.Height.ToString(CultureInfo.InvariantCulture),
            file.ExifOrientation.ToString(CultureInfo.InvariantCulture),
            file.Action.ToString(),
            file.Operation.ToString(),
            file.EdgePolicy.ToString(),
            file.OutputToken,
            file.ExpectedWidth.ToString(CultureInfo.InvariantCulture),
            file.ExpectedHeight.ToString(CultureInfo.InvariantCulture),
            file.ExpectedOrientation.ToString(CultureInfo.InvariantCulture),
            file.ReasonCode ?? "");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

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

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static long ScalarLong(SqliteConnection connection, string sql)
        => (long)(Scalar(connection, sql) ?? 0L);

    private static Result<T> Fail<T>(string code, ErrorCategory category, string message)
        => Result.Fail<T>(code, category, isRetryable: false, message);

    private static string NowUtc()
        => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    private const string SchemaV1Sql = """
        CREATE TABLE jobs(
            job_id TEXT PRIMARY KEY,
            contract_version TEXT NOT NULL,
            created_utc TEXT NOT NULL,
            tool_version TEXT NOT NULL,
            mode TEXT NOT NULL,
            source_root TEXT NOT NULL,
            output_root TEXT NOT NULL,
            config_digest TEXT NOT NULL,
            model_identity TEXT,
            policy_identity TEXT,
            state TEXT NOT NULL,
            plan_seal TEXT,
            sealed_utc TEXT);
        CREATE TABLE plan_rows(
            seq INTEGER PRIMARY KEY,
            job_id TEXT NOT NULL REFERENCES jobs(job_id),
            source_path TEXT NOT NULL,
            size_bytes INTEGER NOT NULL,
            last_write_utc TEXT NOT NULL,
            prefix_sha256 TEXT NOT NULL,
            full_sha256 TEXT,
            volume_serial TEXT NOT NULL,
            file_id_hex TEXT NOT NULL,
            format TEXT NOT NULL,
            width INTEGER NOT NULL,
            height INTEGER NOT NULL,
            exif_orientation INTEGER NOT NULL,
            action TEXT NOT NULL CHECK (action IN ('Keep', 'Normalize', 'Review', 'Unsupported')),
            operation TEXT NOT NULL,
            edge_policy TEXT NOT NULL,
            output_token TEXT NOT NULL,
            expected_width INTEGER NOT NULL,
            expected_height INTEGER NOT NULL,
            expected_orientation INTEGER NOT NULL,
            reason_code TEXT,
            row_digest TEXT NOT NULL);
        CREATE INDEX idx_plan_rows_action ON plan_rows(action);
        CREATE TABLE events(
            event_seq INTEGER PRIMARY KEY AUTOINCREMENT,
            job_id TEXT NOT NULL,
            file_seq INTEGER,
            event_type TEXT NOT NULL,
            result TEXT NOT NULL,
            reason_code TEXT,
            data_json TEXT,
            at_utc TEXT NOT NULL);
        CREATE TABLE journal(
            intent_id TEXT PRIMARY KEY,
            file_seq INTEGER NOT NULL,
            operation TEXT NOT NULL,
            checkpoint TEXT NOT NULL,
            details_json TEXT,
            updated_utc TEXT NOT NULL);
        CREATE TABLE schema_migrations(
            migration_id INTEGER PRIMARY KEY AUTOINCREMENT,
            from_version INTEGER NOT NULL,
            to_version INTEGER NOT NULL,
            tool_version TEXT NOT NULL,
            applied_utc TEXT NOT NULL,
            status TEXT NOT NULL);
        CREATE TRIGGER events_no_update BEFORE UPDATE ON events
            BEGIN SELECT RAISE(ABORT, 'events are append-only'); END;
        CREATE TRIGGER events_no_delete BEFORE DELETE ON events
            BEGIN SELECT RAISE(ABORT, 'events are append-only'); END;
        CREATE TRIGGER plan_rows_sealed_no_update BEFORE UPDATE ON plan_rows
            WHEN (SELECT plan_seal FROM jobs LIMIT 1) IS NOT NULL
            BEGIN SELECT RAISE(ABORT, 'plan is sealed'); END;
        CREATE TRIGGER plan_rows_sealed_no_delete BEFORE DELETE ON plan_rows
            WHEN (SELECT plan_seal FROM jobs LIMIT 1) IS NOT NULL
            BEGIN SELECT RAISE(ABORT, 'plan is sealed'); END;
        CREATE TRIGGER plan_rows_sealed_no_insert BEFORE INSERT ON plan_rows
            WHEN (SELECT plan_seal FROM jobs LIMIT 1) IS NOT NULL
            BEGIN SELECT RAISE(ABORT, 'plan is sealed'); END;
        CREATE TRIGGER jobs_seal_write_once BEFORE UPDATE OF plan_seal ON jobs
            WHEN OLD.plan_seal IS NOT NULL
            BEGIN SELECT RAISE(ABORT, 'plan seal is write-once'); END;
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
