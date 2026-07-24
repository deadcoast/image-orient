using OrientExpress.Application.Filesystem;
using OrientExpress.Application.Jobs;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Jobs;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `upright undo <job>` (PRD-FR-015, AC-029/AC-030): restore verified
/// backups for an in-place job — but ONLY where the file on disk still
/// matches the journal's expected post-apply state. Anything else is a
/// reported conflict and an abstention, never a forced restore. Each
/// restore is itself journaled and idempotent.
/// </summary>
public static class UndoCommand
{
    private const string UndoEventName = "undo.commit";

    public static async Task<int> RunAsync(
        OutputContext context,
        IJobStore store,
        Application.Filesystem.IAtomicFileOperations atomicOperations,
        string jobIdValue,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var job = new JobId(jobIdValue);
        var opened = await store.OpenAsync(job, cancellationToken).ConfigureAwait(false);
        if (!opened.IsSuccess)
        {
            context.WriteError(
                CliExitCode.InvalidArgument, opened.Error!.Code, opened.Error.SafeMessage, "see 'upright jobs list'");
            return (int)CliExitCode.InvalidArgument;
        }

        if (opened.Require().Manifest.Mode != "in-place")
        {
            context.WriteError(
                CliExitCode.InvalidArgument, "NOT_IN_PLACE",
                "undo applies to in-place jobs; safe-copy outputs can simply be deleted",
                "nothing in the sources was ever modified by a safe-copy job");
            return (int)CliExitCode.InvalidArgument;
        }

        if (!confirmed)
        {
            context.WriteError(
                CliExitCode.SafetyPreflightFailed, "ACKNOWLEDGE_REQUIRED",
                "undo rewrites current files from their verified backups", "re-run with --yes");
            return (int)CliExitCode.SafetyPreflightFailed;
        }

        var intents = await store.ReadIntentsAsync(job, unresolvedOnly: false, cancellationToken)
            .ConfigureAwait(false);
        var committed = await store.ReadEventSequencesAsync(job, ApplyCommand.CommitEventName, cancellationToken)
            .ConfigureAwait(false);
        var undone = await store.ReadEventSequencesAsync(job, UndoEventName, cancellationToken)
            .ConfigureAwait(false);
        if (!intents.IsSuccess || !committed.IsSuccess || committed.Require().Count == 0)
        {
            context.WriteError(
                CliExitCode.NothingToDo, "NOTHING_COMMITTED", "this job has no committed replacements to undo",
                "see 'upright jobs show'");
            return (int)CliExitCode.NothingToDo;
        }

        long restored = 0, alreadyUndone = 0;
        var conflicts = new List<object>();
        var rows = new Dictionary<long, PlannedFile>();
        await foreach (var row in store.ReadPlanAsync(
            job, new PlanQuery(PlanAction.Normalize), cancellationToken).ConfigureAwait(false))
        {
            rows[row.Sequence] = row;
        }

        foreach (var intent in intents.Require()
            .Where(i => i.Operation == InPlaceApplier.ReplaceOperation)
            .OrderBy(i => i.FileSequence))
        {
            if (!committed.Require().Contains(intent.FileSequence))
            {
                continue;
            }

            if (undone.IsSuccess && undone.Require().Contains(intent.FileSequence))
            {
                alreadyUndone++;
                continue;
            }

            var details = InPlaceApplier.ParseDetails(intent.DetailsJson);
            if (details?.PostSha is null || !rows.TryGetValue(intent.FileSequence, out var row))
            {
                conflicts.Add(new { sequence = intent.FileSequence, reason = "JOURNAL_DETAILS_MISSING" });
                continue;
            }

            // AC-029: restore only when reality matches the journal.
            var currentSha = File.Exists(row.SourcePath)
                ? FileContentDigest.Sha256OfFile(row.SourcePath)
                : "missing";
            if (currentSha != details.PostSha)
            {
                conflicts.Add(new { sequence = intent.FileSequence, reason = "UNDO_STATE_MISMATCH" });
                continue;
            }

            if (!File.Exists(details.Backup)
                || FileContentDigest.Sha256OfFile(details.Backup) != details.PreSha)
            {
                conflicts.Add(new { sequence = intent.FileSequence, reason = "BACKUP_VERIFY_FAILED" });
                continue;
            }

            var sourcePath = PathCanonicalizer.Normalize(row.SourcePath);
            var stage = StagePathPlanner.PlanStagePath(sourcePath);
            var undoDetails = new InPlaceApplier.IntentDetails(
                details.Backup, details.PostSha, details.PreSha, stage.Display);
            var undoIntent = await store.OpenIntentAsync(
                job,
                new JournalIntent($"undo-{intent.FileSequence:D8}", intent.FileSequence,
                    InPlaceApplier.UndoOperation, JournalCheckpoints.Prepared,
                    InPlaceApplier.Serialize(undoDetails), ""),
                cancellationToken).ConfigureAwait(false);
            if (!undoIntent.IsSuccess)
            {
                conflicts.Add(new { sequence = intent.FileSequence, reason = undoIntent.Error!.Code });
                continue;
            }

            File.Copy(details.Backup, stage.Display);
            _ = (await store.AdvanceIntentAsync(
                job, $"undo-{intent.FileSequence:D8}", JournalCheckpoints.IntentDurable, null, cancellationToken)
                .ConfigureAwait(false)).Require();
            var replaced = atomicOperations.ReplaceExisting(sourcePath, stage);
            if (!replaced.IsSuccess)
            {
                File.Delete(stage.Display);
                _ = await store.AdvanceIntentAsync(
                    job, $"undo-{intent.FileSequence:D8}", JournalCheckpoints.Reconciled, null, cancellationToken)
                    .ConfigureAwait(false);
                conflicts.Add(new { sequence = intent.FileSequence, reason = replaced.Error!.Code });
                continue;
            }

            _ = (await store.AdvanceIntentAsync(
                job, $"undo-{intent.FileSequence:D8}", JournalCheckpoints.Replaced, null, cancellationToken)
                .ConfigureAwait(false)).Require();
            if (FileContentDigest.Sha256OfFile(row.SourcePath) != details.PreSha)
            {
                conflicts.Add(new { sequence = intent.FileSequence, reason = "RECOVERY_REQUIRED" });
                continue;
            }

            _ = (await store.AdvanceIntentAsync(
                job, $"undo-{intent.FileSequence:D8}", JournalCheckpoints.Committed, null, cancellationToken)
                .ConfigureAwait(false)).Require();
            _ = (await store.AppendEventsAsync(
                job, [new JobEvent(UndoEventName, "succeeded", intent.FileSequence)], cancellationToken)
                .ConfigureAwait(false)).Require();
            _ = (await store.AdvanceIntentAsync(
                job, $"undo-{intent.FileSequence:D8}", JournalCheckpoints.Reconciled, null, cancellationToken)
                .ConfigureAwait(false)).Require();
            restored++;
        }

        var document = new
        {
            command = "undo",
            jobId = job.Value,
            counts = new { restored, alreadyUndone, conflicts = conflicts.Count },
            conflicts,
        };
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, document);
        }
        else
        {
            context.Out.WriteLine($"undo {job.Value}: {restored} file(s) restored from verified backups");
            if (alreadyUndone > 0)
            {
                context.Out.WriteLine($"  already undone : {alreadyUndone}");
            }

            foreach (dynamic conflict in conflicts)
            {
                context.Out.WriteLine($"  CONFLICT row {conflict.sequence}: {conflict.reason} (file left as-is)");
            }
        }

        return conflicts.Count > 0
            ? restored > 0 ? (int)CliExitCode.PartialCompletion : (int)CliExitCode.Conflict
            : (int)CliExitCode.Success;
    }
}
