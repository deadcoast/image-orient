using System.Collections.Concurrent;
using System.Threading.Channels;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Pipeline;

namespace OrientExpress.Application.Pipeline;

/// <summary>One bounded stage: a name, limits, retry, and the work itself.</summary>
public sealed record PipelineStage<TItem>(
    string Name,
    StageLimits Limits,
    Func<TItem, CancellationToken, ValueTask<Result<TItem>>> Execute,
    RetryPolicy? Retry = null);

/// <summary>An item that failed a stage, with the stage name and its typed error.</summary>
public sealed record PipelineFailure<TItem>(TItem Item, string Stage, ErrorInfo Error);

public sealed record PipelineSummary<TItem>(
    long Succeeded,
    long Failed,
    bool CancelledGracefully,
    IReadOnlyList<PipelineFailure<TItem>> Failures);

/// <summary>
/// COMP-PIPELINE (PRD-FR-022): items flow through ordered stages, each with
/// its own bounded queue and worker count, so memory stays bounded under
/// backpressure regardless of corpus size (AC-041). An item failing a stage
/// is recorded and leaves the pipeline — one bad file never stops the run.
/// Two-token cancellation (AC-042): the soft token stops intake and lets
/// in-flight items finish (resumable state); the hard token abandons work
/// promptly. Completed items are handed to the terminal consumer as they
/// finish — the pipeline holds no unbounded result list.
/// </summary>
public static class BoundedPipeline
{
    public static async Task<PipelineSummary<TItem>> RunAsync<TItem>(
        IAsyncEnumerable<TItem> source,
        IReadOnlyList<PipelineStage<TItem>> stages,
        Func<TItem, CancellationToken, ValueTask> terminalConsumer,
        IProgressSink progress,
        CancellationToken softCancellation,
        CancellationToken hardAbort)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(stages);
        ArgumentOutOfRangeException.ThrowIfZero(stages.Count);
        ArgumentNullException.ThrowIfNull(terminalConsumer);
        ArgumentNullException.ThrowIfNull(progress);

        var failures = new ConcurrentBag<PipelineFailure<TItem>>();
        var succeeded = 0L;
        var sawSoftCancellation = false;

        // channels[i] feeds stages[i]; the last channel feeds the terminal consumer.
        var channels = new Channel<TItem>[stages.Count + 1];
        for (var index = 0; index < stages.Count; index++)
        {
            channels[index] = Channel.CreateBounded<TItem>(new BoundedChannelOptions(stages[index].Limits.QueueCapacity)
            {
                SingleWriter = false,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait,
            });
        }

        channels[stages.Count] = Channel.CreateBounded<TItem>(new BoundedChannelOptions(
            stages[^1].Limits.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
        });

        var intake = Task.Run(
            async () =>
            {
                try
                {
                    await foreach (var item in source.WithCancellation(hardAbort).ConfigureAwait(false))
                    {
                        if (softCancellation.IsCancellationRequested)
                        {
                            sawSoftCancellation = true;
                            break;
                        }

                        await channels[0].Writer.WriteAsync(item, hardAbort).ConfigureAwait(false);
                    }
                }
                finally
                {
                    channels[0].Writer.Complete();
                }
            },
            hardAbort);

        var stageTasks = new List<Task>();
        for (var stageIndex = 0; stageIndex < stages.Count; stageIndex++)
        {
            var stage = stages[stageIndex];
            var input = channels[stageIndex].Reader;
            var output = channels[stageIndex + 1].Writer;
            var completedInStage = 0L;
            var failedInStage = 0L;
            var workers = new Task[stage.Limits.Workers];
            for (var workerIndex = 0; workerIndex < workers.Length; workerIndex++)
            {
                workers[workerIndex] = Task.Run(
                    async () =>
                    {
                        await foreach (var item in input.ReadAllAsync(hardAbort).ConfigureAwait(false))
                        {
                            var result = await ExecuteWithRetryAsync(stage, item, hardAbort).ConfigureAwait(false);
                            if (result.IsSuccess)
                            {
                                progress.Report(new PipelineProgress(
                                    stage.Name,
                                    Interlocked.Increment(ref completedInStage),
                                    Interlocked.Read(ref failedInStage)));
                                await output.WriteAsync(result.Require(), hardAbort).ConfigureAwait(false);
                            }
                            else
                            {
                                failures.Add(new PipelineFailure<TItem>(item, stage.Name, result.Error!));
                                progress.Report(new PipelineProgress(
                                    stage.Name,
                                    Interlocked.Read(ref completedInStage),
                                    Interlocked.Increment(ref failedInStage)));
                            }
                        }
                    },
                    hardAbort);
            }

            stageTasks.Add(Task.WhenAll(workers).ContinueWith(
                _ => output.Complete(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default));
        }

        var terminal = Task.Run(
            async () =>
            {
                await foreach (var item in channels[stages.Count].Reader.ReadAllAsync(hardAbort).ConfigureAwait(false))
                {
                    await terminalConsumer(item, hardAbort).ConfigureAwait(false);
                    _ = Interlocked.Increment(ref succeeded);
                }
            },
            hardAbort);

        await Task.WhenAll([intake, .. stageTasks, terminal]).ConfigureAwait(false);
        return new PipelineSummary<TItem>(
            Interlocked.Read(ref succeeded),
            failures.Count,
            sawSoftCancellation,
            [.. failures]);
    }

    private static async ValueTask<Result<TItem>> ExecuteWithRetryAsync<TItem>(
        PipelineStage<TItem> stage, TItem item, CancellationToken cancellationToken)
    {
        var policy = stage.Retry ?? RetryPolicy.None;
        Result<TItem> result = default;
        for (var attempt = 1; attempt <= Math.Max(1, policy.MaxAttempts); attempt++)
        {
            result = await stage.Execute(item, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess || result.Error is not { IsRetryable: true })
            {
                return result;
            }

            if (attempt < policy.MaxAttempts && policy.BaseDelayMilliseconds > 0)
            {
                // Exponential backoff: base, 2x, 4x, ...
                await Task.Delay(
                    policy.BaseDelayMilliseconds * (1 << (attempt - 1)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return result;
    }
}
