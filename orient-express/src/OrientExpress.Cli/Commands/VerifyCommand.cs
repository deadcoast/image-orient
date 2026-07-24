using OrientExpress.Application.Filesystem;
using OrientExpress.Application.Jobs;
using OrientExpress.Application.Verify;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Jobs;
using OrientExpress.Contracts.Verify;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `upright verify <job>` (PRD-FR-013, WP-013 command surface): re-verify
/// every committed output through the independent read path, long after
/// apply finished. Read-only; a later corruption or deletion of an output
/// is found here, never papered over.
/// </summary>
public static class VerifyCommand
{
    public static async Task<int> RunAsync(
        OutputContext context,
        IJobStore store,
        IOutputVerifier verifier,
        string jobIdValue,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var job = new JobId(jobIdValue);
        var opened = await store.OpenAsync(job, cancellationToken).ConfigureAwait(false);
        if (!opened.IsSuccess)
        {
            context.WriteError(
                CliExitCode.InvalidArgument, opened.Error!.Code, opened.Error.SafeMessage, "see 'upright jobs list'");
            return (int)CliExitCode.InvalidArgument;
        }

        var committed = await store.ReadEventSequencesAsync(job, "apply.commit", cancellationToken)
            .ConfigureAwait(false);
        if (!committed.IsSuccess || committed.Require().Count == 0)
        {
            context.WriteError(
                CliExitCode.NothingToDo, "NOTHING_COMMITTED",
                "this job has no committed outputs to verify", "run 'upright apply' first");
            return (int)CliExitCode.NothingToDo;
        }

        var outputRoot = opened.Require().Manifest.OutputRoot;
        long verified = 0;
        var failures = new List<object>();
        await foreach (var row in store.ReadPlanAsync(
            job, new PlanQuery(PlanAction.Normalize), cancellationToken).ConfigureAwait(false))
        {
            if (!committed.Require().Contains(row.Sequence))
            {
                continue;
            }

            var destination = PathCanonicalizer.Normalize(Path.Combine(outputRoot, row.OutputToken));
            var result = await verifier.VerifyAsync(
                new VerificationContract("jpeg", row.ExpectedWidth, row.ExpectedHeight, row.ExpectedOrientation),
                destination,
                cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess && result.Require().Passed)
            {
                verified++;
            }
            else
            {
                failures.Add(new
                {
                    sequence = row.Sequence,
                    outputToken = row.OutputToken,
                    reasons = result.IsSuccess ? result.Require().Failures.ToArray() : [result.Error!.Code],
                });
            }
        }

        var document = new
        {
            command = "verify",
            jobId = job.Value,
            outputRoot,
            counts = new { verified, failed = failures.Count },
            failures,
        };
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, document);
        }
        else
        {
            context.Out.WriteLine($"verify {job.Value}: {verified} output(s) still verify");
            foreach (dynamic failure in failures)
            {
                context.Out.WriteLine($"  FAILED row {failure.sequence} ({failure.outputToken}): "
                    + string.Join(',', failure.reasons));
            }
        }

        return failures.Count > 0 ? (int)CliExitCode.VerificationFailed : (int)CliExitCode.Success;
    }
}
