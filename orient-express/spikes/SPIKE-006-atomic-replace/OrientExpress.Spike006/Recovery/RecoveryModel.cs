using OrientExpress.Spike006.Protocol;

namespace OrientExpress.Spike006.Recovery;

public enum SourceCondition
{
    Original,
    ExpectedOutput,
    Tampered,
    Missing,
}

public enum StageCondition
{
    Missing,
    VerifiedExpectedOutput,
    Corrupt,
}

public enum BackupCondition
{
    Missing,
    VerifiedOriginal,
    Corrupt,
}

/// <summary>
/// Recovery decisions from the DOC-FSSAFE recovery matrix. Every action is
/// non-destructive toward user data: nothing here may delete or overwrite a
/// source, backup, or unexplained file.
/// </summary>
public enum RecoveryAction
{
    /// <summary>Pre-intent crash: quarantine any stage remnant; source stays authoritative.</summary>
    QuarantineStageSourceAuthoritative,

    /// <summary>Intent durable, source untouched, stage verified: retry the qualified replacement.</summary>
    RetryReplacement,

    /// <summary>Intent durable and the source already carries the expected output: record the commit after independent verification.</summary>
    RecordRecoveredCommit,

    /// <summary>Intent durable, source untouched, but the stage is unusable: stop with an actionable error; source remains authoritative.</summary>
    StopActionableStageError,

    /// <summary>Observed state matches no safe row: stop and preserve every artifact.</summary>
    StopCriticalConflict,

    /// <summary>Committed and consistent: finishing touches only; safe to repeat.</summary>
    FinalizeIdempotent,

    /// <summary>Committed but the file changed externally afterwards: report drift; never overwrite.</summary>
    ReportDriftWithoutOverwrite,
}

public readonly record struct RecoveryObservation(
    JournalCheckpoint Checkpoint,
    SourceCondition Source,
    StageCondition Stage,
    BackupCondition Backup);
