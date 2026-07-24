using System.Text.Json;
using OrientExpress.Application.Filesystem;
using OrientExpress.Application.Jobs;
using OrientExpress.Application.Transform;
using OrientExpress.Application.Verify;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Jobs;
using OrientExpress.Contracts.Transform;
using OrientExpress.Contracts.Verify;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// PRD-FR-010 guarded in-place execution (SPIKE-006 protocol): verified
/// backup → durable journal intent → transform to a same-directory stage →
/// independent verification → write-through ReplaceFileW → post-replace
/// verification → commit. The journal intent is durable BEFORE the
/// replacement, so a crash at any point is reconciled — never guessed —
/// on the next run. For an in-place job the manifest's output root is the
/// backup root.
/// </summary>
public static class InPlaceApplier
{
    public const string ReplaceOperation = "replace";
    public const string UndoOperation = "undo";

    /// <summary>What the journal remembers about one replacement.</summary>
    public sealed record IntentDetails(string Backup, string PreSha, string? PostSha, string? Stage);

    public static string IntentId(long sequence) => $"replace-{sequence:D8}";

    public static IntentDetails? ParseDetails(string? json)
        => json is null ? null : JsonSerializer.Deserialize<IntentDetails>(json);

    public static string Serialize(IntentDetails details) => JsonSerializer.Serialize(details);

    /// <summary>
    /// DOC-FSSAFE reconcile-first: resolve every unresolved intent before
    /// new work. Crash before Replaced → source must still match its
    /// pre-state (stage deleted, intent closed). Crash after Replaced →
    /// source must match the staged post-state (commit completed). Anything
    /// else is RECOVERY_REQUIRED and stops the run.
    /// </summary>
    public static async Task<Result<int>> ReconcileAsync(
        IJobStore store, JobId job, CancellationToken cancellationToken)
    {
        var unresolved = await store.ReadIntentsAsync(job, unresolvedOnly: true, cancellationToken)
            .ConfigureAwait(false);
        if (!unresolved.IsSuccess)
        {
            return Result.Fail<int>(unresolved.Error!);
        }

        var reconciled = 0;
        foreach (var intent in unresolved.Require())
        {
            var details = ParseDetails(intent.DetailsJson);
            if (details is null)
            {
                return RecoveryRequired(intent.IntentId, "journal details are missing");
            }

            var sourcePath = await SourcePathOfAsync(store, job, intent.FileSequence, cancellationToken)
                .ConfigureAwait(false);
            if (sourcePath is null)
            {
                return RecoveryRequired(intent.IntentId, "no plan row for the intent");
            }

            var currentSha = File.Exists(sourcePath) ? FileContentDigest.Sha256OfFile(sourcePath) : "missing";
            if (intent.Checkpoint is JournalCheckpoints.Prepared
                or JournalCheckpoints.StagedVerified
                or JournalCheckpoints.IntentDurable)
            {
                if (currentSha != details.PreSha)
                {
                    return RecoveryRequired(intent.IntentId, "source no longer matches its pre-replace state");
                }

                if (details.Stage is not null && File.Exists(details.Stage))
                {
                    File.Delete(details.Stage);
                }

                _ = (await store.AdvanceIntentAsync(
                    job, intent.IntentId, JournalCheckpoints.Reconciled, intent.DetailsJson, cancellationToken)
                    .ConfigureAwait(false)).Require();
                _ = await store.AppendEventsAsync(
                    job,
                    [new JobEvent("recovery.aborted_intent", "succeeded", intent.FileSequence)],
                    cancellationToken).ConfigureAwait(false);
                reconciled++;
                continue;
            }

            // Checkpoint Replaced: the swap happened; only completion is missing.
            if (currentSha == details.PostSha)
            {
                _ = await store.AppendEventsAsync(
                    job,
                    [new JobEvent(ApplyCommand.CommitEventName, "succeeded", intent.FileSequence, "RECOVERED")],
                    cancellationToken).ConfigureAwait(false);
                _ = (await store.AdvanceIntentAsync(
                    job, intent.IntentId, JournalCheckpoints.Reconciled, intent.DetailsJson, cancellationToken)
                    .ConfigureAwait(false)).Require();
                reconciled++;
                continue;
            }

            return RecoveryRequired(intent.IntentId, "source matches neither pre- nor post-replace state");
        }

        return Result.Ok(reconciled);

        static Result<int> RecoveryRequired(string intentId, string why)
            => Result.Fail<int>(
                "RECOVERY_REQUIRED", ErrorCategory.IntegrityViolation, false,
                $"intent '{intentId}': {why}; resolve manually before applying");
    }

    /// <summary>One guarded in-place row. Every failure path closes its intent or leaves it for reconcile.</summary>
    public static async ValueTask<Result<PlannedFile>> ApplyRowAsync(
        IJobStore store,
        JobId job,
        IFileIdentityService identity,
        IImageTransformer transformer,
        IOutputVerifier verifier,
        IAtomicFileOperations atomicOperations,
        string backupRoot,
        PlannedFile row,
        CancellationToken cancellationToken)
    {
        if (row.FullSha256 is null)
        {
            return Fail("FINGERPRINT_TOO_WEAK",
                "in-place apply requires the strong fingerprint captured by 'scan --mode in-place'");
        }

        var live = await identity.CaptureAsync(
            PathCanonicalizer.Normalize(row.SourcePath), FingerprintStrength.Strong, cancellationToken)
            .ConfigureAwait(false);
        if (!live.IsSuccess)
        {
            return Result.Fail<PlannedFile>(live.Error!);
        }

        var drift = StaleInputCheck.FindDrift(row, live.Require());
        if (drift.Count > 0)
        {
            return Fail("PLAN_INPUT_STALE", $"source changed after scan: {string.Join(',', drift)}");
        }

        // Verified backup before anything else (AC-018).
        var backupPath = Path.Combine(backupRoot, row.OutputToken);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        if (!File.Exists(backupPath))
        {
            File.Copy(row.SourcePath, backupPath);
        }

        if (FileContentDigest.Sha256OfFile(backupPath) != row.FullSha256)
        {
            return Fail("BACKUP_VERIFY_FAILED", "the backup copy does not match the source fingerprint");
        }

        var sourcePath = PathCanonicalizer.Normalize(row.SourcePath);
        var stage = new StagingTarget(StagePathPlanner.PlanStagePath(sourcePath));
        var details = new IntentDetails(backupPath, row.FullSha256, PostSha: null, stage.Path.Display);
        var intent = await store.OpenIntentAsync(
            job,
            new JournalIntent(IntentId(row.Sequence), row.Sequence, ReplaceOperation,
                JournalCheckpoints.Prepared, Serialize(details), ""),
            cancellationToken).ConfigureAwait(false);
        if (!intent.IsSuccess)
        {
            return Result.Fail<PlannedFile>(intent.Error!);
        }

        var plan = new TransformPlan(
            row.Operation, row.EdgePolicy, row.ExpectedOrientation, row.ExpectedWidth, row.ExpectedHeight);
        var transformed = await transformer.TransformAsync(live.Require(), stage, plan, cancellationToken)
            .ConfigureAwait(false);
        if (!transformed.IsSuccess)
        {
            return await AbortAsync(transformed.Error!);
        }

        var verified = await verifier.VerifyAsync(
            new VerificationContract("jpeg", row.ExpectedWidth, row.ExpectedHeight, row.ExpectedOrientation),
            stage.Path,
            cancellationToken).ConfigureAwait(false);
        if (!verified.IsSuccess || !verified.Require().Passed)
        {
            var reasons = verified.IsSuccess ? string.Join(',', verified.Require().Failures) : verified.Error!.Code;
            return await AbortAsync(new ErrorInfo(
                "VERIFICATION_FAILED", ErrorCategory.IntegrityViolation, false,
                $"staged output failed verification: {reasons}"));
        }

        details = details with { PostSha = FileContentDigest.Sha256OfFile(stage.Path.Display) };
        _ = (await store.AdvanceIntentAsync(
            job, IntentId(row.Sequence), JournalCheckpoints.StagedVerified, Serialize(details), cancellationToken)
            .ConfigureAwait(false)).Require();

        // The intent is durable BEFORE the swap (AC-018 ordering).
        _ = (await store.AdvanceIntentAsync(
            job, IntentId(row.Sequence), JournalCheckpoints.IntentDurable, null, cancellationToken)
            .ConfigureAwait(false)).Require();

        var replaced = atomicOperations.ReplaceExisting(sourcePath, stage.Path);
        if (!replaced.IsSuccess)
        {
            return await AbortAsync(replaced.Error!);
        }

        _ = (await store.AdvanceIntentAsync(
            job, IntentId(row.Sequence), JournalCheckpoints.Replaced, null, cancellationToken)
            .ConfigureAwait(false)).Require();

        // Post-replace verification through the independent path; a mismatch
        // here leaves the intent at Replaced for reconcile — never guessed.
        if (FileContentDigest.Sha256OfFile(row.SourcePath) != details.PostSha)
        {
            return Fail("RECOVERY_REQUIRED", "the replaced file does not match the verified stage content");
        }

        _ = (await store.AdvanceIntentAsync(
            job, IntentId(row.Sequence), JournalCheckpoints.Committed, null, cancellationToken)
            .ConfigureAwait(false)).Require();
        _ = (await store.AdvanceIntentAsync(
            job, IntentId(row.Sequence), JournalCheckpoints.Reconciled, null, cancellationToken)
            .ConfigureAwait(false)).Require();
        return Result.Ok(row);

        async ValueTask<Result<PlannedFile>> AbortAsync(ErrorInfo error)
        {
            DeleteQuietly(stage.Path.Display);
            _ = await store.AdvanceIntentAsync(
                job, IntentId(row.Sequence), JournalCheckpoints.Reconciled,
                Serialize(details with { Stage = null }), cancellationToken).ConfigureAwait(false);
            return Result.Fail<PlannedFile>(error);
        }

        static Result<PlannedFile> Fail(string code, string message)
            => Result.Fail<PlannedFile>(code, ErrorCategory.InvalidInput, false, message);
    }

    private static async Task<string?> SourcePathOfAsync(
        IJobStore store, JobId job, long sequence, CancellationToken cancellationToken)
    {
        await foreach (var row in store.ReadPlanAsync(
            job, new PlanQuery(AfterSequence: sequence - 1, Limit: 1), cancellationToken).ConfigureAwait(false))
        {
            return row.Sequence == sequence ? row.SourcePath : null;
        }

        return null;
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Orphan cleanup keys on journal ownership (DOC-FSSAFE).
        }
    }
}
