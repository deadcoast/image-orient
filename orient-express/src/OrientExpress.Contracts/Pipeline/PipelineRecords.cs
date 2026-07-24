namespace OrientExpress.Contracts.Pipeline;

/// <summary>
/// Ephemeral human progress (DOC-COMPONENTS: events are durable facts;
/// progress is UI and may be disabled). Implementations must be cheap and
/// non-blocking — the pipeline calls this from worker threads.
/// </summary>
public interface IProgressSink
{
    public void Report(PipelineProgress progress);
}

public sealed record PipelineProgress(string Stage, long Completed, long Failed);

public sealed class NullProgressSink : IProgressSink
{
    public static NullProgressSink Instance { get; } = new();

    public void Report(PipelineProgress progress)
    {
    }
}

/// <summary>Bounded-stage limits (PRD-FR-022), sourced from DOC-CONFIG `performance.*`.</summary>
public sealed record StageLimits(int Workers, int QueueCapacity)
{
    public static StageLimits Sanitize(int? workers, int? queueCapacity)
        => new(Math.Clamp(workers ?? 1, 1, 64), Math.Clamp(queueCapacity ?? 64, 1, 65_536));
}

/// <summary>
/// Bounded retry for items whose typed error says it is worth it
/// (`ErrorInfo.IsRetryable`, e.g. sharing violations). Everything else
/// fails once, loudly.
/// </summary>
public sealed record RetryPolicy(int MaxAttempts, int BaseDelayMilliseconds)
{
    public static RetryPolicy None { get; } = new(1, 0);
}
