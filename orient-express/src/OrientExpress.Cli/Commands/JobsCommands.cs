using OrientExpress.Application.Jobs;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Jobs;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `upright jobs list|show|integrity|prune` (PRD-FR-028, AC-049/AC-050):
/// inspect and maintain jobs without ever applying image changes. Prune is
/// dry-run by default and refuses jobs with pending recovery work.
/// </summary>
public static class JobsCommands
{
    public static async Task<int> ListAsync(OutputContext context, IJobStore store, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var jobs = await store.ListAsync(cancellationToken).ConfigureAwait(false);
        if (!jobs.IsSuccess)
        {
            context.WriteError(CliExitCode.InternalError, jobs.Error!.Code, jobs.Error.SafeMessage, "check the job root");
            return (int)CliExitCode.InternalError;
        }

        var rows = jobs.Require()
            .Select(handle => new
            {
                jobId = handle.Id.Value,
                created = handle.Manifest.CreatedUtc,
                mode = handle.Manifest.Mode,
                sourceRoot = handle.Manifest.SourceRoot,
                outputRoot = handle.Manifest.OutputRoot,
            })
            .ToArray();
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new { command = "jobs list", jobs = rows });
        }
        else if (rows.Length == 0)
        {
            context.Out.WriteLine("no jobs; 'upright scan' creates them");
        }
        else
        {
            foreach (var row in rows)
            {
                context.Out.WriteLine($"{row.jobId}  [{row.mode}]  {row.sourceRoot} -> {row.outputRoot}");
            }
        }

        return (int)CliExitCode.Success;
    }

    public static async Task<int> ShowAsync(
        OutputContext context, IJobStore store, string jobIdValue, CancellationToken cancellationToken)
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

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        await foreach (var row in store.ReadPlanAsync(job, new PlanQuery(), cancellationToken).ConfigureAwait(false))
        {
            counts[row.Action.ToString()] = counts.GetValueOrDefault(row.Action.ToString()) + 1;
        }

        var committed = await store.ReadEventSequencesAsync(job, "apply.commit", cancellationToken)
            .ConfigureAwait(false);
        var seal = await store.VerifySealAsync(job, cancellationToken).ConfigureAwait(false);
        var manifest = opened.Require().Manifest;
        var document = new
        {
            command = "jobs show",
            jobId = job.Value,
            manifest.CreatedUtc,
            manifest.Mode,
            manifest.SourceRoot,
            manifest.OutputRoot,
            manifest.ToolVersion,
            configDigest = manifest.ConfigDigest,
            sealed_ = seal.IsSuccess && seal.Require() is not null,
            planSeal = seal.IsSuccess ? seal.Require()?.Sha256 : null,
            planCounts = counts,
            committedRows = committed.IsSuccess ? committed.Require().Count : 0,
        };
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, document);
        }
        else
        {
            context.Out.WriteLine($"{job.Value} [{manifest.Mode}] created {manifest.CreatedUtc}");
            context.Out.WriteLine($"  source : {manifest.SourceRoot}");
            context.Out.WriteLine($"  output : {manifest.OutputRoot}");
            context.Out.WriteLine($"  sealed : {(document.sealed_ ? document.planSeal![..16] + "…" : "NO")}");
            foreach (var pair in counts.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                context.Out.WriteLine($"  plan.{pair.Key,-11}: {pair.Value}");
            }

            context.Out.WriteLine($"  committed      : {document.committedRows}");
        }

        return (int)CliExitCode.Success;
    }

    public static async Task<int> IntegrityAsync(
        OutputContext context, IJobStore store, string? jobIdValue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var targets = new List<JobId>();
        if (jobIdValue is not null)
        {
            targets.Add(new JobId(jobIdValue));
        }
        else
        {
            var all = await store.ListAsync(cancellationToken).ConfigureAwait(false);
            targets.AddRange(all.IsSuccess ? all.Require().Select(handle => handle.Id) : []);
        }

        if (targets.Count == 0)
        {
            context.WriteError(CliExitCode.NothingToDo, "NO_JOBS", "no jobs to check", "'upright scan' creates jobs");
            return (int)CliExitCode.NothingToDo;
        }

        var results = new List<object>();
        var anyProblem = false;
        foreach (var job in targets)
        {
            var report = await store.CheckIntegrityAsync(job, cancellationToken).ConfigureAwait(false);
            if (!report.IsSuccess)
            {
                anyProblem = true;
                results.Add(new { jobId = job.Value, ok = false, problems = new[] { report.Error!.Code } });
                continue;
            }

            var value = report.Require();
            anyProblem |= value.Problems.Count > 0;
            results.Add(new
            {
                jobId = job.Value,
                ok = value.Problems.Count == 0,
                databaseOk = value.DatabaseOk,
                sealPresent = value.SealPresent,
                sealMatches = value.SealMatches,
                fileCount = value.FileCount,
                eventCount = value.EventCount,
                problems = value.Problems.ToArray(),
            });
        }

        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new { command = "jobs integrity", results });
        }
        else
        {
            foreach (dynamic row in results)
            {
                context.Out.WriteLine($"{row.jobId}: {(row.ok ? "ok" : "PROBLEMS: " + string.Join(',', row.problems))}");
            }
        }

        return anyProblem ? (int)CliExitCode.VerificationFailed : (int)CliExitCode.Success;
    }

    public static async Task<int> PruneAsync(
        OutputContext context, IJobStore store, string jobIdValue, bool confirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var result = await store.PruneAsync(new JobId(jobIdValue), dryRun: !confirmed, cancellationToken)
            .ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            var exit = result.Error!.Code == "JOB_RECOVERY_PENDING" ? CliExitCode.Conflict : CliExitCode.InvalidArgument;
            context.WriteError(exit, result.Error.Code, result.Error.SafeMessage, "see 'upright jobs integrity'");
            return (int)exit;
        }

        if (context.Machine)
        {
            MachineWriter.WriteDocument(
                context, new { command = "jobs prune", jobId = jobIdValue, dryRun = !confirmed, result = result.Require() });
        }
        else
        {
            context.Out.WriteLine(result.Require());
            if (!confirmed)
            {
                context.Out.WriteLine("dry-run is the default (AC-050); pass --yes to remove");
            }
        }

        return (int)CliExitCode.Success;
    }
}
