using OrientExpress.Spike006.Protocol;

namespace OrientExpress.Spike006.Recovery;

public sealed record RecoveryOutcome(
    RecoveryAction Action,
    RecoveryObservation Observation,
    string? StagePath,
    string Notes);

/// <summary>
/// Observes post-crash filesystem state, plans via the proven state machine,
/// and executes the decision. Execution never deletes user data: unexplained
/// artifacts are preserved or quarantined, never removed.
/// </summary>
public static class RecoveryExecutor
{
    public static RecoveryObservation Observe(JobManifest manifest, out string? stagePath)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var checkpoint = new CommitJournal(manifest.JournalPath).ReadLastDurableCheckpoint();
        var source = ClassifySource(manifest);
        var stage = ClassifyStage(manifest, out stagePath);
        var backup = ClassifyBackup(manifest);
        return new RecoveryObservation(checkpoint, source, stage, backup);
    }

    public static RecoveryOutcome Recover(JobManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var observation = Observe(manifest, out var stagePath);
        var action = RecoveryPlanner.Plan(observation);
        var notes = Execute(manifest, observation, action, stagePath);
        return new RecoveryOutcome(action, observation, stagePath, notes);
    }

    private static string Execute(
        JobManifest manifest, RecoveryObservation observation, RecoveryAction action, string? stagePath)
    {
        var journal = new CommitJournal(manifest.JournalPath);
        var record = new JournalRecord(
            Checkpoint: nameof(JournalCheckpoint.None),
            JobId: manifest.JobId,
            SourcePath: manifest.SourcePath,
            StagePath: stagePath ?? string.Empty,
            BackupPath: manifest.BackupPath,
            OriginalSha256: manifest.OriginalSha256,
            ExpectedSha256: manifest.ExpectedSha256);

        switch (action)
        {
            case RecoveryAction.QuarantineStageSourceAuthoritative:
                if (stagePath is null)
                {
                    return "no stage remnant; source authoritative";
                }

                Directory.CreateDirectory(manifest.QuarantineDirectory);
                var quarantined = Path.Combine(manifest.QuarantineDirectory, Path.GetFileName(stagePath));
                File.Move(stagePath, quarantined, overwrite: false);
                return $"stage quarantined to '{quarantined}'; source authoritative";

            case RecoveryAction.RetryReplacement:
                InPlaceCommitProtocol.Replace(manifest.SourcePath, stagePath!);
                VerifyRecoveredSource(manifest);
                journal.Append(JournalCheckpoint.Committed, record);
                journal.Append(JournalCheckpoint.Finalized, record);
                return "replacement retried, verified, and committed";

            case RecoveryAction.RecordRecoveredCommit:
                VerifyRecoveredSource(manifest);
                journal.Append(JournalCheckpoint.Committed, record);
                journal.Append(JournalCheckpoint.Finalized, record);
                return "existing replacement independently verified; commit recorded";

            case RecoveryAction.FinalizeIdempotent:
                if (observation.Checkpoint == JournalCheckpoint.Committed)
                {
                    journal.Append(JournalCheckpoint.Finalized, record);
                    return "finalize checkpoint recorded";
                }

                return "already finalized; no action";

            case RecoveryAction.ReportDriftWithoutOverwrite:
                return "post-commit external change reported; nothing overwritten";

            case RecoveryAction.StopActionableStageError:
                return "stage unusable; stopped with source authoritative; artifacts preserved";

            case RecoveryAction.StopCriticalConflict:
                return "critical conflict; stopped; all artifacts preserved";

            default:
                throw new InvalidOperationException($"Unhandled recovery action {action}.");
        }
    }

    private static void VerifyRecoveredSource(JobManifest manifest)
    {
        var digest = Digests.Sha256HexOfFile(manifest.SourcePath);
        if (digest != manifest.ExpectedSha256)
        {
            throw new InvalidOperationException("recovered source failed independent verification");
        }
    }

    private static SourceCondition ClassifySource(JobManifest manifest)
    {
        if (!File.Exists(manifest.SourcePath))
        {
            return SourceCondition.Missing;
        }

        var digest = Digests.Sha256HexOfFile(manifest.SourcePath);
        if (digest == manifest.OriginalSha256)
        {
            return SourceCondition.Original;
        }

        return digest == manifest.ExpectedSha256 ? SourceCondition.ExpectedOutput : SourceCondition.Tampered;
    }

    private static StageCondition ClassifyStage(JobManifest manifest, out string? stagePath)
    {
        var pattern = Path.GetFileName(manifest.SourcePath) + InPlaceCommitProtocol.StageMarker + "*";
        var candidates = Directory.GetFiles(manifest.SourceDirectory, pattern);
        if (candidates.Length == 0)
        {
            stagePath = null;
            return StageCondition.Missing;
        }

        stagePath = candidates[0];
        return Digests.Sha256HexOfFile(stagePath) == manifest.ExpectedSha256
            ? StageCondition.VerifiedExpectedOutput
            : StageCondition.Corrupt;
    }

    private static BackupCondition ClassifyBackup(JobManifest manifest)
    {
        if (!File.Exists(manifest.BackupPath))
        {
            return BackupCondition.Missing;
        }

        return Digests.Sha256HexOfFile(manifest.BackupPath) == manifest.OriginalSha256
            ? BackupCondition.VerifiedOriginal
            : BackupCondition.Corrupt;
    }
}
