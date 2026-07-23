using OrientExpress.Spike006.Protocol;

namespace OrientExpress.Spike006.Recovery;

/// <summary>
/// Pure decision function for the DOC-FSSAFE recovery matrix. The fallback for
/// any observation outside the proven rows is a critical-conflict stop, never
/// an action that touches user data.
/// </summary>
public static class RecoveryPlanner
{
    public static RecoveryAction Plan(RecoveryObservation observation)
        => observation switch
        {
            { Checkpoint: JournalCheckpoint.Finalized }
                => RecoveryAction.FinalizeIdempotent,

            { Checkpoint: JournalCheckpoint.None, Source: SourceCondition.Original }
                => RecoveryAction.QuarantineStageSourceAuthoritative,

            { Checkpoint: JournalCheckpoint.None }
                => RecoveryAction.StopCriticalConflict,

            { Checkpoint: JournalCheckpoint.IntentDurable, Source: SourceCondition.Original, Stage: StageCondition.VerifiedExpectedOutput }
                => RecoveryAction.RetryReplacement,

            { Checkpoint: JournalCheckpoint.IntentDurable, Source: SourceCondition.Original }
                => RecoveryAction.StopActionableStageError,

            // The matrix row assumes a verified backup; the observed backup
            // condition is recorded alongside so a damaged backup surfaces in
            // evidence, but it does not change this non-destructive decision.
            { Checkpoint: JournalCheckpoint.IntentDurable, Source: SourceCondition.ExpectedOutput }
                => RecoveryAction.RecordRecoveredCommit,

            { Checkpoint: JournalCheckpoint.IntentDurable }
                => RecoveryAction.StopCriticalConflict,

            { Checkpoint: JournalCheckpoint.Committed, Source: SourceCondition.ExpectedOutput }
                => RecoveryAction.FinalizeIdempotent,

            { Checkpoint: JournalCheckpoint.Committed }
                => RecoveryAction.ReportDriftWithoutOverwrite,

            _ => RecoveryAction.StopCriticalConflict,
        };
}
