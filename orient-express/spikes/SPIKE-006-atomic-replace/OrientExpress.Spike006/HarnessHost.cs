using System.Globalization;
using OrientExpress.Spike006.Protocol;
using OrientExpress.Spike006.Reporting;
using OrientExpress.Spike006.Scenarios;

namespace OrientExpress.Spike006;

/// <summary>Command dispatch for the SPIKE-006 harness.</summary>
public static class HarnessHost
{
    public static int Run(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args switch
        {
            ["run-all", .. var rest] => RunAll(ParseOptions(rest)),
            ["run-protocol", .. var rest] => RunProtocolChild(ParseOptions(rest)),
            _ => Usage(),
        };
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: spike006 run-all [--work <dir>] [--results <file>]");
        Console.Error.WriteLine("       spike006 run-protocol --job <dir> --fault <FaultPoint>");
        return 2;
    }

    private static Dictionary<string, string> ParseOptions(string[] rest)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < rest.Length; index += 2)
        {
            if (rest[index].StartsWith("--", StringComparison.Ordinal))
            {
                options[rest[index][2..]] = rest[index + 1];
            }
        }

        return options;
    }

    private static int RunAll(Dictionary<string, string> options)
    {
        var workRoot = options.TryGetValue("work", out var work)
            ? work
            : Path.Combine(
                Path.GetTempPath(),
                "spike006-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        var resultsPath = options.TryGetValue("results", out var results) ? results : "spike006-results.json";
        Directory.CreateDirectory(workRoot);
        Console.WriteLine($"spike006: work root {workRoot}");

        var scenarios = new List<ScenarioResult>();
        scenarios.AddRange(PrimitiveScenarios.RunAll(workRoot));
        scenarios.AddRange(FaultScenarios.RunAll(workRoot));

        var report = RunReportWriter.Build(workRoot, scenarios);
        RunReportWriter.Write(report, resultsPath);

        foreach (var scenario in scenarios)
        {
            Console.WriteLine($"{scenario.Outcome,-7} safety={(scenario.SourceSafetyHeld ? "held" : "LOST")} {scenario.Id}");
        }

        var summary = report.Summary;
        Console.WriteLine(
            $"spike006: total={summary.Total} pass={summary.Pass} fail={summary.Fail} " +
            $"skipped={summary.Skipped} source-loss={summary.SourceLossCases}");
        Console.WriteLine($"spike006: results written to {Path.GetFullPath(resultsPath)}");
        return summary.Fail == 0 && summary.SourceLossCases == 0 ? 0 : 1;
    }

    private static int RunProtocolChild(Dictionary<string, string> options)
    {
        if (!options.TryGetValue("job", out var jobDirectory) || !options.TryGetValue("fault", out var faultName))
        {
            return Usage();
        }

        if (!Enum.TryParse<FaultPoint>(faultName, ignoreCase: false, out var fault))
        {
            Console.Error.WriteLine($"spike006: unknown fault point '{faultName}'.");
            return 2;
        }

        var manifest = JobFixture.Load(jobDirectory);
        InPlaceCommitProtocol.Run(manifest, new FaultInjector(fault));
        return 0;
    }
}
