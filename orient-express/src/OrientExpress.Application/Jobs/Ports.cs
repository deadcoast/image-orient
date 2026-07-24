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

    public ValueTask<Result<IntegrityReport>> CheckIntegrityAsync(JobId job, CancellationToken cancellationToken);

    public ValueTask<Result<IReadOnlyList<JobHandle>>> ListAsync(CancellationToken cancellationToken);
}
