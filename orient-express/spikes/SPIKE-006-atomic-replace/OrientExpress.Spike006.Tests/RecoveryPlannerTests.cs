using OrientExpress.Spike006.Protocol;
using OrientExpress.Spike006.Recovery;

namespace OrientExpress.Spike006.Tests;

/// <summary>
/// Encodes the DOC-FSSAFE recovery matrix row by row. The planner must be a
/// total function whose every branch is non-destructive toward user data.
/// </summary>
public sealed class RecoveryPlannerTests
{
    private static RecoveryAction Plan(
        JournalCheckpoint checkpoint,
        SourceCondition source,
        StageCondition stage = StageCondition.Missing,
        BackupCondition backup = BackupCondition.Missing)
        => RecoveryPlanner.Plan(new RecoveryObservation(checkpoint, source, stage, backup));

    [Theory]
    [InlineData(StageCondition.Missing)]
    [InlineData(StageCondition.VerifiedExpectedOutput)]
    [InlineData(StageCondition.Corrupt)]
    public void Before_intent_with_original_source_quarantines_stage_and_keeps_source_authoritative(StageCondition stage)
        => Assert.Equal(
            RecoveryAction.QuarantineStageSourceAuthoritative,
            Plan(JournalCheckpoint.None, SourceCondition.Original, stage));

    [Theory]
    [InlineData(SourceCondition.ExpectedOutput)]
    [InlineData(SourceCondition.Tampered)]
    [InlineData(SourceCondition.Missing)]
    public void Before_intent_with_unexplained_source_state_stops_as_critical_conflict(SourceCondition source)
        => Assert.Equal(
            RecoveryAction.StopCriticalConflict,
            Plan(JournalCheckpoint.None, source, StageCondition.VerifiedExpectedOutput));

    [Fact]
    public void Intent_durable_with_original_source_and_verified_stage_retries_replacement()
        => Assert.Equal(
            RecoveryAction.RetryReplacement,
            Plan(
                JournalCheckpoint.IntentDurable,
                SourceCondition.Original,
                StageCondition.VerifiedExpectedOutput,
                BackupCondition.VerifiedOriginal));

    [Theory]
    [InlineData(StageCondition.Missing)]
    [InlineData(StageCondition.Corrupt)]
    public void Intent_durable_with_original_source_but_unusable_stage_stops_with_actionable_error(StageCondition stage)
        => Assert.Equal(
            RecoveryAction.StopActionableStageError,
            Plan(JournalCheckpoint.IntentDurable, SourceCondition.Original, stage, BackupCondition.VerifiedOriginal));

    [Theory]
    [InlineData(BackupCondition.VerifiedOriginal)]
    [InlineData(BackupCondition.Missing)]
    [InlineData(BackupCondition.Corrupt)]
    public void Intent_durable_with_expected_output_records_recovered_commit(BackupCondition backup)
        => Assert.Equal(
            RecoveryAction.RecordRecoveredCommit,
            Plan(JournalCheckpoint.IntentDurable, SourceCondition.ExpectedOutput, StageCondition.Missing, backup));

    [Theory]
    [InlineData(SourceCondition.Tampered)]
    [InlineData(SourceCondition.Missing)]
    public void Intent_durable_with_neither_original_nor_expected_source_stops_as_critical_conflict(SourceCondition source)
        => Assert.Equal(
            RecoveryAction.StopCriticalConflict,
            Plan(JournalCheckpoint.IntentDurable, source, StageCondition.Missing, BackupCondition.VerifiedOriginal));

    [Fact]
    public void Committed_with_expected_output_finalizes_idempotently()
        => Assert.Equal(
            RecoveryAction.FinalizeIdempotent,
            Plan(JournalCheckpoint.Committed, SourceCondition.ExpectedOutput, backup: BackupCondition.VerifiedOriginal));

    [Theory]
    [InlineData(SourceCondition.Tampered, BackupCondition.VerifiedOriginal)]
    [InlineData(SourceCondition.Tampered, BackupCondition.Missing)]
    [InlineData(SourceCondition.Missing, BackupCondition.VerifiedOriginal)]
    [InlineData(SourceCondition.Original, BackupCondition.VerifiedOriginal)]
    public void Committed_with_externally_changed_source_reports_drift_and_never_overwrites(
        SourceCondition source, BackupCondition backup)
        => Assert.Equal(
            RecoveryAction.ReportDriftWithoutOverwrite,
            Plan(JournalCheckpoint.Committed, source, backup: backup));

    [Theory]
    [InlineData(SourceCondition.Original)]
    [InlineData(SourceCondition.ExpectedOutput)]
    [InlineData(SourceCondition.Tampered)]
    [InlineData(SourceCondition.Missing)]
    public void Finalized_jobs_are_terminal_no_ops(SourceCondition source)
        => Assert.Equal(
            RecoveryAction.FinalizeIdempotent,
            Plan(JournalCheckpoint.Finalized, source));

    [Fact]
    public void Planner_is_total_over_the_observation_space()
    {
        foreach (var checkpoint in Enum.GetValues<JournalCheckpoint>())
        {
            foreach (var source in Enum.GetValues<SourceCondition>())
            {
                foreach (var stage in Enum.GetValues<StageCondition>())
                {
                    foreach (var backup in Enum.GetValues<BackupCondition>())
                    {
                        var action = RecoveryPlanner.Plan(new RecoveryObservation(checkpoint, source, stage, backup));
                        Assert.True(Enum.IsDefined(action));
                    }
                }
            }
        }
    }
}
