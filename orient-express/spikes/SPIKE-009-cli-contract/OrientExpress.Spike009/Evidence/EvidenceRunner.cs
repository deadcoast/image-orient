using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using OrientExpress.Spike009.Contract;

namespace OrientExpress.Spike009.Evidence;

/// <summary>
/// Out-of-process qualification of the machine contract: BOM/UTF-8, stream
/// separation, exit-code registry, destructive guard, sealed-plan staleness,
/// response files, completion determinism, and cancellation.
/// </summary>
public static class EvidenceRunner
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        var resultsPath = "spike009-results.json";
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            if (args[index] == "--results")
            {
                resultsPath = args[index + 1];
            }
        }

        var workRoot = Path.Combine(
            Path.GetTempPath(),
            "spike009-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(workRoot);

        List<Func<string, ScenarioResult>> scenarios =
        [
            JsonlStreamIsParseCleanUtf8NoBom,
            JsonModeEmitsExactlyOneDocument,
            MachineStdoutStaysPureWhileProgressGoesToStderr,
            ExitCodeMatrix,
            DestructiveGuardRequiresAcknowledgement,
            TamperedPlanIsRefusedAsStale,
            ResponseFileExpansionIsEquivalent,
            CompletionScriptIsDeterministic,
            HumanModeContractAndNoAnsi,
            HelpAndCancellation,
            CorrelationIdPropagates,
        ];

        var results = new List<ScenarioResult>();
        foreach (var scenario in scenarios)
        {
            var directory = Path.Combine(workRoot, scenario.Method.Name);
            Directory.CreateDirectory(directory);
            try
            {
                results.Add(scenario(directory));
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException)
            {
                results.Add(new ScenarioRecorder(scenario.Method.Name, "harness", "unexpected failure").Failed(exception));
            }
        }

        WriteReport(results, resultsPath, workRoot);
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Outcome,-5} {result.Id}");
        }

        var failed = results.Count(result => result.Outcome == ScenarioResult.Fail);
        Console.WriteLine($"spike009: total={results.Count} pass={results.Count - failed} fail={failed}");
        Console.WriteLine($"spike009: results written to {Path.GetFullPath(resultsPath)}");
        return failed == 0 ? 0 : 1;
    }

    private static ScenarioResult JsonlStreamIsParseCleanUtf8NoBom(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-JSONL-PARSE-CLEAN", "machine-stream",
            "JSONL stdout is BOM-free strict UTF-8; every line is an independently parseable schema-shaped event");
        var child = ChildProcess.Run(
            "scan", "café-rot90.jpg", "review-Ünïcøde.jpg", "keep.jpg", "--jsonl", "--non-interactive");

        recorder.Measure("exit_code", child.ExitCode);
        recorder.Expect("exit_review_required", child.ExitCode == (int)CliExitCode.ReviewRequired);
        recorder.Expect("no_bom", !child.StdoutHasBom());

        var text = child.StdoutStrictUtf8();
        recorder.Expect("strict_utf8_decode", true);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r')).ToList();
        recorder.Measure("line_count", lines.Count);
        var allParse = true;
        var allShaped = true;
        var decodedTokens = new List<string>();
        foreach (var line in lines)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                foreach (var field in new[] { "schemaVersion", "eventType", "eventId", "timestampUtc", "result" })
                {
                    allShaped &= document.RootElement.TryGetProperty(field, out _);
                }

                if (document.RootElement.TryGetProperty("pathToken", out var pathToken))
                {
                    decodedTokens.Add(pathToken.GetString() ?? string.Empty);
                }
            }
            catch (JsonException)
            {
                allParse = false;
            }
        }

        recorder.Expect("every_line_parses", allParse);
        recorder.Expect("required_event_fields_present", allShaped);
        // System.Text.Json escapes non-ASCII (café); the decoded value is the contract.
        recorder.Expect("unicode_round_trip", decodedTokens.Contains("café-rot90.jpg"));
        recorder.Measure("non_ascii_is_escaped_on_the_wire", !text.Contains('é', StringComparison.Ordinal));
        return recorder.Complete();
    }

    private static ScenarioResult JsonModeEmitsExactlyOneDocument(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-JSON-SINGLE-DOC", "machine-stream", "JSON mode emits exactly one parseable document on stdout");
        var child = ChildProcess.Run("scan", "a-rot90.jpg", "b-rot180.jpg", "--json", "--non-interactive");
        recorder.Expect("exit_success", child.ExitCode == (int)CliExitCode.Success);
        recorder.Expect("no_bom", !child.StdoutHasBom());
        try
        {
            using var document = JsonDocument.Parse(child.StdoutStrictUtf8());
            recorder.Expect("single_document_parses", true);
            recorder.Expect(
                "declares_result_type",
                document.RootElement.GetProperty("resultType").GetString() == "command.result");
        }
        catch (JsonException)
        {
            recorder.Expect("single_document_parses", false);
        }

        return recorder.Complete();
    }

    private static ScenarioResult MachineStdoutStaysPureWhileProgressGoesToStderr(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-STREAM-SEPARATION", "machine-stream",
            "progress goes to stderr in interactive mode and machine stdout stays parse-clean");
        var child = ChildProcess.Run("scan", "a-rot90.jpg", "b.jpg", "--jsonl");
        recorder.Expect("stderr_has_progress", child.Stderr.Contains("progress:", StringComparison.Ordinal));
        var lines = child.StdoutStrictUtf8().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        recorder.Expect(
            "stdout_lines_all_json",
            lines.All(line => line.TrimEnd('\r').StartsWith('{')));

        var quiet = ChildProcess.Run("scan", "a-rot90.jpg", "--jsonl", "--non-interactive");
        recorder.Expect(
            "non_interactive_suppresses_progress",
            !quiet.Stderr.Contains("progress:", StringComparison.Ordinal));
        return recorder.Complete();
    }

    private static ScenarioResult ExitCodeMatrix(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-EXIT-MATRIX", "exit-codes", "registry values for success, review, partial, nothing, capability, grammar");
        recorder.Expect(
            "success_0",
            ChildProcess.Run("scan", "a-rot90.jpg", "--json", "--non-interactive").ExitCode == 0);
        recorder.Expect(
            "review_10",
            ChildProcess.Run("scan", "b-review.jpg", "--json", "--non-interactive").ExitCode == 10);
        recorder.Expect(
            "partial_11",
            ChildProcess.Run("scan", "c-error.jpg", "d-rot90.jpg", "--json", "--non-interactive").ExitCode == 11);
        recorder.Expect(
            "nothing_12",
            ChildProcess.Run("scan", "keep.jpg", "--json", "--non-interactive").ExitCode == 12);
        recorder.Expect(
            "capability_22",
            ChildProcess.Run("scan", "e-unsupported.heic", "--json", "--non-interactive").ExitCode == 22);
        recorder.Expect(
            "grammar_20_unknown_option",
            ChildProcess.Run("scan", "a.jpg", "--wat").ExitCode == 20);
        recorder.Expect(
            "grammar_20_missing_paths",
            ChildProcess.Run("scan").ExitCode == 20);
        recorder.Expect(
            "grammar_20_bad_format",
            ChildProcess.Run("scan", "a.jpg", "--output-format", "xml").ExitCode == 20);
        return recorder.Complete();
    }

    private static ScenarioResult DestructiveGuardRequiresAcknowledgement(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-DESTRUCTIVE-GUARD", "safety", "in-place plans refuse to apply without --acknowledge-in-place");
        var jobDirectory = Path.Combine(directory, "job");
        var sealResult = ChildProcess.Run(
            "scan", "a-rot90.jpg", "--job", jobDirectory, "--mode", "in-place", "--json", "--non-interactive");
        recorder.Expect("plan_sealed", sealResult.ExitCode == 0);

        var refused = ChildProcess.Run("apply", jobDirectory, "--json", "--non-interactive");
        recorder.Expect("refused_without_flag", refused.ExitCode == (int)CliExitCode.InvalidArgument);
        recorder.Expect(
            "refusal_names_the_flag",
            refused.Stderr.Contains("--acknowledge-in-place", StringComparison.Ordinal));
        recorder.Expect(
            "refusal_carries_reason_code",
            refused.Stderr.Contains("IN_PLACE_NOT_ACKNOWLEDGED", StringComparison.Ordinal));

        var allowed = ChildProcess.Run(
            "apply", jobDirectory, "--acknowledge-in-place", "--json", "--non-interactive");
        recorder.Expect("proceeds_with_flag", allowed.ExitCode == 0);
        return recorder.Complete();
    }

    private static ScenarioResult TamperedPlanIsRefusedAsStale(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-STALE-PLAN", "safety", "a tampered sealed plan is refused with STALE_PLAN, not partially applied");
        var jobDirectory = Path.Combine(directory, "job");
        _ = ChildProcess.Run("scan", "a-rot90.jpg", "--job", jobDirectory, "--json", "--non-interactive");

        var planPath = Path.Combine(jobDirectory, "plan.json");
        File.WriteAllText(
            planPath,
            File.ReadAllText(planPath).Replace("ROTATE_90_CW", "ROTATE_180", StringComparison.Ordinal));

        var child = ChildProcess.Run("apply", jobDirectory, "--json", "--non-interactive");
        recorder.Expect("stale_plan_30", child.ExitCode == (int)CliExitCode.StalePlan);
        recorder.Expect("names_stale_plan", child.Stderr.Contains("STALE_PLAN", StringComparison.Ordinal));
        return recorder.Complete();
    }

    private static ScenarioResult ResponseFileExpansionIsEquivalent(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-RESPONSE-FILE", "automation", "@file arguments expand to the same contract as direct arguments");
        var responsePath = Path.Combine(directory, "args.rsp");
        File.WriteAllLines(
            responsePath,
            ["# spike009 response file", "scan", "a-rot90.jpg", "b-review.jpg", "--json", "--non-interactive"],
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var direct = ChildProcess.Run("scan", "a-rot90.jpg", "b-review.jpg", "--json", "--non-interactive");
        var expanded = ChildProcess.Run("@" + responsePath);
        recorder.Expect("same_exit_code", direct.ExitCode == expanded.ExitCode);
        recorder.Measure("exit_code", direct.ExitCode);
        recorder.Expect(
            "missing_response_file_is_grammar_error",
            ChildProcess.Run("@" + Path.Combine(directory, "absent.rsp")).ExitCode == 20);
        return recorder.Complete();
    }

    private static ScenarioResult CompletionScriptIsDeterministic(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-COMPLETION", "automation", "PowerShell completion derives from the command model and is deterministic");
        var first = ChildProcess.Run("completion", "powershell");
        var second = ChildProcess.Run("completion", "powershell");
        recorder.Expect("exit_success", first.ExitCode == 0);
        recorder.Expect("byte_identical_runs", first.StdoutBytes.AsSpan().SequenceEqual(second.StdoutBytes));
        var script = first.StdoutStrictUtf8();
        foreach (var token in new[] { "scan", "apply", "doctor", "--acknowledge-in-place", "--output-format" })
        {
            recorder.Expect("contains_" + token.Replace("--", "opt_", StringComparison.Ordinal), script.Contains(token, StringComparison.Ordinal));
        }

        recorder.Expect(
            "unsupported_shell_is_grammar_error",
            ChildProcess.Run("completion", "bash").ExitCode == 20);
        return recorder.Complete();
    }

    private static ScenarioResult HumanModeContractAndNoAnsi(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-HUMAN-CONTRACT", "human-output", "first line names command/mode/job; redirected output carries no ANSI");
        var child = ChildProcess.Run("scan", "a-rot90.jpg", "--non-interactive");
        var firstLine = child.StdoutStrictUtf8().Split('\n')[0].TrimEnd('\r');
        recorder.Measure("first_line", firstLine);
        recorder.Expect(
            "first_line_contract",
            firstLine.StartsWith(CliHost.ExecutableName + " scan — mode=", StringComparison.Ordinal));
        recorder.Expect("no_ansi_stdout", !child.StdoutBytes.Contains((byte)0x1B));
        recorder.Expect("no_ansi_stderr", !child.Stderr.Contains('', StringComparison.Ordinal));
        return recorder.Complete();
    }

    private static ScenarioResult HelpAndCancellation(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-HELP-AND-CANCEL", "automation", "--help exits 0 with usage; simulated cancellation exits 40 with a cancelled event");
        var help = ChildProcess.Run("--help");
        recorder.Expect("help_exit_0", help.ExitCode == 0);
        recorder.Expect(
            "help_shows_usage",
            help.StdoutStrictUtf8().Contains("Usage", StringComparison.OrdinalIgnoreCase));

        var cancelled = ChildProcess.Run(
            "scan", "a-rot90.jpg", "b-rot90.jpg", "c-rot90.jpg",
            "--simulate-cancel-after", "2", "--jsonl", "--non-interactive");
        recorder.Expect("cancel_exit_40", cancelled.ExitCode == (int)CliExitCode.Cancelled);
        var lines = cancelled.StdoutStrictUtf8().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        recorder.Expect(
            "final_event_cancelled",
            lines[^1].Contains("\"result\":\"cancelled\"", StringComparison.Ordinal));
        return recorder.Complete();
    }

    private static ScenarioResult CorrelationIdPropagates(string directory)
    {
        var recorder = new ScenarioRecorder(
            "E-CORRELATION", "automation", "caller-provided correlation id is echoed on every machine event");
        var child = ChildProcess.Run(
            "scan", "a-rot90.jpg", "b.jpg", "--jsonl", "--non-interactive",
            "--correlation-id", "spike009-trace-1");
        var lines = child.StdoutStrictUtf8().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        recorder.Expect(
            "every_event_carries_correlation_id",
            lines.All(line => line.Contains("\"correlationId\":\"spike009-trace-1\"", StringComparison.Ordinal)));
        return recorder.Complete();
    }

    private static void WriteReport(List<ScenarioResult> results, string resultsPath, string workRoot)
    {
        var report = new
        {
            spike = "SPIKE-009",
            environment = new
            {
                osDescription = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                dependency = "System.CommandLine 2.0.10 (DEP-002)",
                contractVersion = CliHost.ContractVersion,
                harnessVersion = "0.1.0",
                workRoot,
                runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            },
            scenarios = results,
            summary = new
            {
                total = results.Count,
                pass = results.Count(result => result.Outcome == ScenarioResult.Pass),
                fail = results.Count(result => result.Outcome == ScenarioResult.Fail),
            },
        };
        var directory = Path.GetDirectoryName(Path.GetFullPath(resultsPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(
            resultsPath,
            JsonSerializer.Serialize(report, ReportOptions) + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
