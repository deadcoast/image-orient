using System.Text.Json;
using OrientExpress.Application.Filesystem;
using OrientExpress.Application.Jobs;
using OrientExpress.Application.Pipeline;
using OrientExpress.Application.Transform;
using OrientExpress.Application.Verify;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Jobs;
using OrientExpress.Contracts.Pipeline;
using OrientExpress.Contracts.Transform;
using OrientExpress.Contracts.Verify;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `upright apply <job>` (PRD-FR-008/011/012/013, safe-copy only): consumes
/// the sealed plan and never re-decides. Per row: refuse stale inputs by
/// recorded identity (AC-015), transform losslessly into an exclusive stage
/// file beside the destination, verify through the independent read path
/// (AC-025), then commit with the atomic create-path move. Sources are
/// never written. A failed row is recorded and the run continues; --resume
/// skips rows whose commit event already exists (AC-028).
/// </summary>
public static class ApplyCommand
{
    private const string CommitEvent = "apply.commit";

    private static readonly JsonSerializerOptions ReportJson = new() { WriteIndented = true };

    public static async Task<int> RunAsync(
        OutputContext context,
        IJobStore store,
        IFileIdentityService identity,
        IImageTransformer transformer,
        IOutputVerifier verifier,
        IAtomicFileOperations atomicOperations,
        string jobIdValue,
        bool resume,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var job = new JobId(jobIdValue);
        var opened = await store.OpenAsync(job, cancellationToken).ConfigureAwait(false);
        if (!opened.IsSuccess)
        {
            context.WriteError(
                CliExitCode.InvalidArgument, opened.Error!.Code, opened.Error.SafeMessage,
                "check the job id; scan creates jobs");
            return (int)CliExitCode.InvalidArgument;
        }

        var manifest = opened.Require().Manifest;
        if (manifest.Mode != "safe-copy")
        {
            context.WriteError(
                CliExitCode.CapabilityUnavailable, "IN_PLACE_GATED",
                "in-place apply is gated until WP-012/WP-015 evidence exists", "use a safe-copy job");
            return (int)CliExitCode.CapabilityUnavailable;
        }

        var seal = await store.VerifySealAsync(job, cancellationToken).ConfigureAwait(false);
        if (!seal.IsSuccess)
        {
            context.WriteError(
                CliExitCode.StalePlan, seal.Error!.Code, seal.Error.SafeMessage,
                "the job database changed after sealing; re-run scan");
            return (int)CliExitCode.StalePlan;
        }

        if (seal.Require() is null)
        {
            context.WriteError(
                CliExitCode.StalePlan, "PLAN_NOT_SEALED",
                "this job has no sealed plan", "re-run scan to produce a sealed plan");
            return (int)CliExitCode.StalePlan;
        }

        var committed = resume
            ? (await store.ReadEventSequencesAsync(job, CommitEvent, cancellationToken).ConfigureAwait(false)).Require()
            : new HashSet<long>();

        _ = await store.AppendEventsAsync(
            job, [new JobEvent("apply.started", resume ? "resumed" : "running")], cancellationToken)
            .ConfigureAwait(false);

        long applied = 0, resumeSkipped = 0, planned = 0;
        var summary = await BoundedPipeline.RunAsync(
            ReadNormalizeRows(store, job, cancellationToken),
            [
                new PipelineStage<PlannedFile>(
                    "apply",
                    new StageLimits(Workers: 2, QueueCapacity: 16),
                    async (row, ct) =>
                    {
                        if (committed.Contains(row.Sequence))
                        {
                            return Result.Ok(row with { ReasonCode = "RESUME_SKIP" });
                        }

                        return await ApplyRowAsync(
                            identity, transformer, verifier, atomicOperations, manifest, row, ct)
                            .ConfigureAwait(false);
                    }),
            ],
            async (row, ct) =>
            {
                if (row.ReasonCode == "RESUME_SKIP")
                {
                    resumeSkipped++;
                    return;
                }

                applied++;
                _ = (await store.AppendEventsAsync(
                    job,
                    [new JobEvent(CommitEvent, "succeeded", row.Sequence)],
                    ct).ConfigureAwait(false)).Require();
            },
            NullProgressSink.Instance,
            cancellationToken,
            CancellationToken.None);

        await foreach (var _ in store.ReadPlanAsync(
            job, new PlanQuery(PlanAction.Normalize), cancellationToken).ConfigureAwait(false))
        {
            planned++;
        }

        foreach (var failure in summary.Failures)
        {
            _ = await store.AppendEventsAsync(
                job,
                [new JobEvent("apply.row_failed", "failed", failure.Item.Sequence, failure.Error.Code)],
                cancellationToken).ConfigureAwait(false);
        }

        var exitCode = Summarize(planned, applied, resumeSkipped, summary);
        _ = await store.AppendEventsAsync(
            job,
            [new JobEvent("apply.completed", exitCode == (int)CliExitCode.Success ? "succeeded" : "partial")],
            cancellationToken).ConfigureAwait(false);

        var report = new
        {
            command = "apply",
            jobId = job.Value,
            mode = "safe-copy",
            outputRoot = manifest.OutputRoot,
            counts = new
            {
                planned,
                applied,
                resumeSkipped,
                failed = summary.Failed,
            },
            failures = summary.Failures
                .Select(f => new { sequence = f.Item.Sequence, reason = f.Error.Code })
                .OrderBy(f => f.sequence)
                .ToArray(),
        };
        await File.WriteAllTextAsync(
            Path.Combine(opened.Require().JobDirectory, "reports", "apply-summary.json"),
            JsonSerializer.Serialize(report, ReportJson) + "\n",
            cancellationToken).ConfigureAwait(false);

        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, report);
        }
        else
        {
            context.Out.WriteLine($"apply {job.Value} [safe-copy] into {manifest.OutputRoot}");
            context.Out.WriteLine($"  rotated + verified + committed : {applied}");
            if (resumeSkipped > 0)
            {
                context.Out.WriteLine($"  already done (resume)          : {resumeSkipped}");
            }

            if (summary.Failed > 0)
            {
                context.Out.WriteLine($"  failed (sources untouched)     : {summary.Failed}");
                foreach (var failure in summary.Failures.OrderBy(f => f.Item.Sequence).Take(10))
                {
                    context.Out.WriteLine($"    row {failure.Item.Sequence}: {failure.Error.Code}");
                }
            }
        }

        return exitCode;
    }

    private static async ValueTask<Result<PlannedFile>> ApplyRowAsync(
        IFileIdentityService identity,
        IImageTransformer transformer,
        IOutputVerifier verifier,
        IAtomicFileOperations atomicOperations,
        JobManifest manifest,
        PlannedFile row,
        CancellationToken cancellationToken)
    {
        // AC-015: the file on disk must still be the file that was scanned.
        var live = await identity.CaptureAsync(
            PathCanonicalizer.Normalize(row.SourcePath),
            row.FullSha256 is null ? FingerprintStrength.Standard : FingerprintStrength.Strong,
            cancellationToken).ConfigureAwait(false);
        if (!live.IsSuccess)
        {
            return Result.Fail<PlannedFile>(live.Error!);
        }

        var drift = StaleInputCheck.FindDrift(row, live.Require());
        if (drift.Count > 0)
        {
            return Result.Fail<PlannedFile>(
                "PLAN_INPUT_STALE", ErrorCategory.InvalidInput, false,
                $"source changed after scan: {string.Join(',', drift)}");
        }

        var destination = Path.Combine(manifest.OutputRoot, row.OutputToken);
        if (File.Exists(destination))
        {
            return Result.Fail<PlannedFile>(
                "DESTINATION_EXISTS", ErrorCategory.InvalidInput, false,
                "the destination already exists (--on-collision error is the safe-copy default)");
        }

        _ = Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var destinationPath = PathCanonicalizer.Normalize(destination);
        var stage = new StagingTarget(StagePathPlanner.PlanStagePath(destinationPath));
        var plan = new TransformPlan(
            row.Operation, row.EdgePolicy, row.ExpectedOrientation, row.ExpectedWidth, row.ExpectedHeight);
        var transformed = await transformer.TransformAsync(live.Require(), stage, plan, cancellationToken)
            .ConfigureAwait(false);
        if (!transformed.IsSuccess)
        {
            return Result.Fail<PlannedFile>(transformed.Error!);
        }

        // AC-025/026: independent reopen; a failed verification never commits
        // and never touches the source.
        var verified = await verifier.VerifyAsync(
            new VerificationContract("jpeg", row.ExpectedWidth, row.ExpectedHeight, row.ExpectedOrientation),
            stage.Path,
            cancellationToken).ConfigureAwait(false);
        if (!verified.IsSuccess || !verified.Require().Passed)
        {
            DeleteQuietly(stage.Path.Display);
            var reasons = verified.IsSuccess
                ? string.Join(',', verified.Require().Failures)
                : verified.Error!.Code;
            return Result.Fail<PlannedFile>(
                "VERIFICATION_FAILED", ErrorCategory.IntegrityViolation, false,
                $"staged output failed verification: {reasons}");
        }

        var moved = atomicOperations.MoveToNewDestination(stage.Path, destinationPath);
        if (!moved.IsSuccess)
        {
            DeleteQuietly(stage.Path.Display);
            return Result.Fail<PlannedFile>(moved.Error!);
        }

        return Result.Ok(row);
    }

    private static int Summarize(long planned, long applied, long resumeSkipped, PipelineSummary<PlannedFile> summary)
    {
        if (planned == 0 || (applied == 0 && summary.Failed == 0))
        {
            return (int)CliExitCode.NothingToDo;
        }

        if (summary.Failed == 0)
        {
            return (int)CliExitCode.Success;
        }

        return applied == 0 && summary.Failures.All(f => f.Error.Code == "PLAN_INPUT_STALE")
            ? (int)CliExitCode.StalePlan
            : (int)CliExitCode.PartialCompletion;
    }

    private static async IAsyncEnumerable<PlannedFile> ReadNormalizeRows(
        IJobStore store,
        JobId job,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var row in store.ReadPlanAsync(
            job, new PlanQuery(PlanAction.Normalize), cancellationToken).ConfigureAwait(false))
        {
            yield return row;
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Orphan cleanup owns leftovers; commit already failed loudly.
        }
    }
}
