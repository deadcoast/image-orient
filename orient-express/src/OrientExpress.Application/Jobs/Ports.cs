using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Jobs;

namespace OrientExpress.Application.Jobs;

/// <summary>
/// COMP-JOBSTORE port (DOC-COMPONENTS `IJobStore`): SQLite-canonical job
/// state with append-only events, sealed immutable plans, and integrity
/// diagnostics. Error semantics are normative — a sealed plan refuses
/// mutation (PLAN_SEALED), an unsupported schema version is diagnosed and
/// never guessed (JOB_SCHEMA_UNSUPPORTED).
/// </summary>
public interface IJobStore : IAsyncDisposable
{
    public ValueTask<Result<JobHandle>> CreateAsync(JobManifest manifest, CancellationToken cancellationToken);

    public ValueTask<Result<JobHandle>> OpenAsync(JobId job, CancellationToken cancellationToken);

    public ValueTask<Result<long>> AppendEventsAsync(
        JobId job, IReadOnlyList<JobEvent> events, CancellationToken cancellationToken);

    public ValueTask<Result<long>> UpsertScanBatchAsync(
        JobId job, IReadOnlyList<PlannedFile> files, CancellationToken cancellationToken);

    public ValueTask<Result<PlanSeal>> SealPlanAsync(JobId job, CancellationToken cancellationToken);

    public ValueTask<Result<PlanSeal?>> VerifySealAsync(JobId job, CancellationToken cancellationToken);

    public IAsyncEnumerable<PlannedFile> ReadPlanAsync(JobId job, PlanQuery query, CancellationToken cancellationToken);

    public ValueTask<Result<IReadOnlySet<long>>> ReadEventSequencesAsync(
        JobId job, string eventType, CancellationToken cancellationToken);

    public ValueTask<Result<IntegrityReport>> CheckIntegrityAsync(JobId job, CancellationToken cancellationToken);

    public ValueTask<Result<IReadOnlyList<JobHandle>>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Opens a durable intent at checkpoint Prepared (journal transaction commits before any replacement).</summary>
    public ValueTask<Result<bool>> OpenIntentAsync(
        JobId job, JournalIntent intent, CancellationToken cancellationToken);

    /// <summary>Advances an intent to a strictly later checkpoint; regression is refused by the schema.</summary>
    public ValueTask<Result<bool>> AdvanceIntentAsync(
        JobId job, string intentId, string checkpoint, string? detailsJson, CancellationToken cancellationToken);

    /// <summary>All intents, or only unresolved ones (checkpoint before Committed/Reconciled).</summary>
    public ValueTask<Result<IReadOnlyList<JournalIntent>>> ReadIntentsAsync(
        JobId job, bool unresolvedOnly, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a job directory. Dry-run reports what would be removed;
    /// unresolved journal intents refuse with JOB_RECOVERY_PENDING (AC-050).
    /// </summary>
    public ValueTask<Result<string>> PruneAsync(JobId job, bool dryRun, CancellationToken cancellationToken);
}
