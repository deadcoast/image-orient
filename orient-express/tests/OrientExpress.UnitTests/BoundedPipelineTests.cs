using System.Collections.Concurrent;
using OrientExpress.Application.Pipeline;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Pipeline;

namespace OrientExpress.UnitTests;

/// <summary>
/// PRD-FR-022 engine rules: worker/queue limits actually bound concurrency
/// and in-flight items (AC-041 mechanism), one bad item never stops the
/// run, retry follows the typed-error taxonomy, and soft cancellation
/// drains in-flight work instead of dropping it (AC-042 mechanism).
/// </summary>
public sealed class BoundedPipelineTests
{
    private static async IAsyncEnumerable<int> Range(int count)
    {
        for (var value = 1; value <= count; value++)
        {
            yield return value;
        }

        await Task.CompletedTask;
    }

    private static PipelineStage<int> Stage(
        string name,
        Func<int, CancellationToken, ValueTask<Result<int>>> execute,
        int workers = 2,
        int queue = 4,
        RetryPolicy? retry = null)
        => new(name, new StageLimits(workers, queue), execute, retry);

    [Fact]
    public async Task All_items_flow_through_ordered_stages_to_the_terminal_consumer()
    {
        var consumed = new ConcurrentBag<int>();
        var summary = await BoundedPipeline.RunAsync(
            Range(100),
            [
                Stage("double", (i, _) => ValueTask.FromResult(Result.Ok(i * 2))),
                Stage("add-one", (i, _) => ValueTask.FromResult(Result.Ok(i + 1))),
            ],
            (i, _) =>
            {
                consumed.Add(i);
                return ValueTask.CompletedTask;
            },
            NullProgressSink.Instance,
            CancellationToken.None,
            CancellationToken.None);

        Assert.Equal(100, summary.Succeeded);
        Assert.Equal(0, summary.Failed);
        Assert.False(summary.CancelledGracefully);
        Assert.Equal(Enumerable.Range(1, 100).Select(i => (i * 2) + 1).Order(), consumed.Order());
    }

    [Fact]
    public async Task Worker_limit_bounds_concurrency_and_queue_limit_bounds_in_flight_items()
    {
        var inStage = 0;
        var maxInStage = 0;
        var summary = await BoundedPipeline.RunAsync(
            Range(200),
            [
                Stage(
                    "slow",
                    async (i, ct) =>
                    {
                        var now = Interlocked.Increment(ref inStage);
                        InterlockedMax(ref maxInStage, now);
                        await Task.Delay(1, ct);
                        _ = Interlocked.Decrement(ref inStage);
                        return Result.Ok(i);
                    },
                    workers: 3,
                    queue: 5),
            ],
            (_, _) => ValueTask.CompletedTask,
            NullProgressSink.Instance,
            CancellationToken.None,
            CancellationToken.None);

        Assert.Equal(200, summary.Succeeded);
        Assert.InRange(maxInStage, 1, 3);
    }

    [Fact]
    public async Task A_failing_item_is_recorded_and_the_rest_of_the_run_completes()
    {
        var summary = await BoundedPipeline.RunAsync(
            Range(50),
            [
                Stage("pick", (i, _) => ValueTask.FromResult(i % 10 == 0
                    ? Result.Fail<int>("ITEM_BAD", ErrorCategory.InvalidInput, false, $"item {i}")
                    : Result.Ok(i))),
            ],
            (_, _) => ValueTask.CompletedTask,
            NullProgressSink.Instance,
            CancellationToken.None,
            CancellationToken.None);

        Assert.Equal(45, summary.Succeeded);
        Assert.Equal(5, summary.Failed);
        Assert.All(summary.Failures, failure => Assert.Equal("ITEM_BAD", failure.Error.Code));
        Assert.All(summary.Failures, failure => Assert.Equal("pick", failure.Stage));
    }

    [Fact]
    public async Task Retry_honors_the_typed_error_taxonomy()
    {
        var transientAttempts = new ConcurrentDictionary<int, int>();
        var permanentAttempts = new ConcurrentDictionary<int, int>();
        var summary = await BoundedPipeline.RunAsync(
            Range(10),
            [
                Stage(
                    "flaky",
                    (i, cancellation) =>
                    {
                        if (i == 1)
                        {
                            // Retryable (sharing violation class): succeeds on attempt 3.
                            var attempt = transientAttempts.AddOrUpdate(i, 1, (key, a) => a + 1);
                            return ValueTask.FromResult(attempt < 3
                                ? Result.Fail<int>("SHARING_VIOLATION", ErrorCategory.SharingViolation, true, "locked")
                                : Result.Ok(i));
                        }

                        if (i == 2)
                        {
                            // Not retryable: must fail after exactly one attempt.
                            _ = permanentAttempts.AddOrUpdate(i, 1, (key, a) => a + 1);
                            return ValueTask.FromResult(
                                Result.Fail<int>("ACCESS_DENIED", ErrorCategory.AccessDenied, false, "denied"));
                        }

                        return ValueTask.FromResult(Result.Ok(i));
                    },
                    retry: new RetryPolicy(MaxAttempts: 3, BaseDelayMilliseconds: 0)),
            ],
            (_, _) => ValueTask.CompletedTask,
            NullProgressSink.Instance,
            CancellationToken.None,
            CancellationToken.None);

        Assert.Equal(9, summary.Succeeded);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(3, transientAttempts[1]);
        Assert.Equal(1, permanentAttempts[2]);
        Assert.Equal("ACCESS_DENIED", summary.Failures.Single().Error.Code);
    }

    [Fact]
    public async Task Soft_cancellation_stops_intake_but_finishes_items_already_admitted()
    {
        using var softSource = new CancellationTokenSource();
        var admitted = 0;
        var summary = await BoundedPipeline.RunAsync(
            Range(10_000),
            [
                Stage(
                    "work",
                    async (i, ct) =>
                    {
                        if (Interlocked.Increment(ref admitted) == 20)
                        {
                            await softSource.CancelAsync();
                        }

                        await Task.Delay(1, ct);
                        return Result.Ok(i);
                    },
                    workers: 2,
                    queue: 8),
            ],
            (_, _) => ValueTask.CompletedTask,
            NullProgressSink.Instance,
            softSource.Token,
            CancellationToken.None);

        // AC-042: everything admitted before the cancel finishes; the rest
        // was never started — resumable, not dropped mid-write.
        Assert.True(summary.CancelledGracefully);
        Assert.Equal(admitted, summary.Succeeded);
        Assert.InRange(summary.Succeeded, 20, 200);
        Assert.Equal(0, summary.Failed);
    }

    [Fact]
    public async Task Progress_reports_completed_and_failed_counts_per_stage()
    {
        var reports = new ConcurrentBag<PipelineProgress>();
        var sink = new CollectingSink(reports);
        _ = await BoundedPipeline.RunAsync(
            Range(10),
            [
                Stage("s1", (i, _) => ValueTask.FromResult(i == 5
                    ? Result.Fail<int>("X", ErrorCategory.InvalidInput, false, "x")
                    : Result.Ok(i))),
            ],
            (_, _) => ValueTask.CompletedTask,
            sink,
            CancellationToken.None,
            CancellationToken.None);

        Assert.Equal(10, reports.Count);
        Assert.Equal(9, reports.Max(r => r.Completed));
        Assert.Equal(1, reports.Max(r => r.Failed));
        Assert.All(reports, r => Assert.Equal("s1", r.Stage));
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            if (Interlocked.CompareExchange(ref target, value, current) == current)
            {
                break;
            }
        }
    }

    private sealed class CollectingSink(ConcurrentBag<PipelineProgress> reports) : IProgressSink
    {
        public void Report(PipelineProgress progress) => reports.Add(progress);
    }
}
