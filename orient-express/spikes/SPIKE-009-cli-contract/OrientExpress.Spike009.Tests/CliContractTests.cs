using System.Text.Json;
using OrientExpress.Spike009;
using OrientExpress.Spike009.Contract;

namespace OrientExpress.Spike009.Tests;

/// <summary>
/// Golden tests for the DOC-CLI machine contract, invoked in-process so they
/// run in CI on every change. Byte-level encoding checks live in the
/// out-of-process evidence runner.
/// </summary>
public sealed class CliContractTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("spike009-tests-").FullName;

    private static (int ExitCode, string Stdout, string Stderr) Invoke(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliHost.Invoke(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Theory]
    [InlineData(0, "a-rot90.jpg")]
    [InlineData(10, "a-review.jpg")]
    [InlineData(11, "a-error.jpg", "b-rot90.jpg")]
    [InlineData(11, "a-error.jpg", "b-review.jpg")]
    [InlineData(12, "keep.jpg")]
    [InlineData(22, "a-unsupported.heic")]
    public void Exit_codes_follow_the_registry(int expected, params string[] paths)
    {
        var result = Invoke(["scan", .. paths, "--json", "--non-interactive"]);
        Assert.Equal(expected, result.ExitCode);
    }

    [Fact]
    public void Unknown_option_is_invalid_argument()
        => Assert.Equal((int)CliExitCode.InvalidArgument, Invoke("scan", "a.jpg", "--wat").ExitCode);

    [Fact]
    public void Conflicting_format_shortcuts_are_invalid()
        => Assert.Equal((int)CliExitCode.InvalidArgument, Invoke("scan", "a.jpg", "--json", "--jsonl").ExitCode);

    [Fact]
    public void Jsonl_stdout_is_line_parseable_events_only()
    {
        var result = Invoke("scan", "a-rot90.jpg", "b-review.jpg", "--jsonl", "--non-interactive");

        var lines = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            foreach (var field in new[] { "schemaVersion", "eventType", "eventId", "timestampUtc", "result" })
            {
                Assert.True(document.RootElement.TryGetProperty(field, out _), $"missing {field}");
            }
        }
    }

    [Fact]
    public void Json_stdout_is_exactly_one_document()
    {
        var result = Invoke("scan", "a-rot90.jpg", "--json", "--non-interactive");

        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal("command.result", document.RootElement.GetProperty("resultType").GetString());
        Assert.Equal("SUCCESS", document.RootElement.GetProperty("exitSymbol").GetString());
    }

    [Fact]
    public void Machine_stdout_contains_no_prose()
    {
        var result = Invoke("scan", "a-rot90.jpg", "b.jpg", "--jsonl", "--non-interactive");
        Assert.All(
            result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            line => Assert.StartsWith("{", line.TrimEnd('\r'), StringComparison.Ordinal));
    }

    [Fact]
    public void Human_first_line_names_command_and_mode()
    {
        var result = Invoke("scan", "a-rot90.jpg", "--non-interactive");
        Assert.StartsWith("upright-proto scan — mode=safe-copy", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Correlation_id_appears_on_every_event()
    {
        var result = Invoke(
            "scan", "a-rot90.jpg", "b.jpg", "--jsonl", "--non-interactive", "--correlation-id", "t-1");
        Assert.All(
            result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            line => Assert.Contains("\"correlationId\":\"t-1\"", line, StringComparison.Ordinal));
    }

    [Fact]
    public void In_place_apply_requires_acknowledgement()
    {
        var job = Path.Combine(_directory, "job-guard");
        Assert.Equal(0, Invoke("scan", "a-rot90.jpg", "--job", job, "--mode", "in-place", "--json", "--non-interactive").ExitCode);

        var refused = Invoke("apply", job, "--json", "--non-interactive");
        Assert.Equal((int)CliExitCode.InvalidArgument, refused.ExitCode);
        Assert.Contains("IN_PLACE_NOT_ACKNOWLEDGED", refused.Stderr, StringComparison.Ordinal);
        Assert.Contains("--acknowledge-in-place", refused.Stderr, StringComparison.Ordinal);

        Assert.Equal(0, Invoke("apply", job, "--acknowledge-in-place", "--json", "--non-interactive").ExitCode);
    }

    [Fact]
    public void Tampered_plan_is_refused_as_stale()
    {
        var job = Path.Combine(_directory, "job-stale");
        Assert.Equal(0, Invoke("scan", "a-rot90.jpg", "--job", job, "--json", "--non-interactive").ExitCode);

        var planPath = Path.Combine(job, "plan.json");
        File.WriteAllText(
            planPath,
            File.ReadAllText(planPath).Replace("ROTATE_90_CW", "ROTATE_180", StringComparison.Ordinal));

        Assert.Equal((int)CliExitCode.StalePlan, Invoke("apply", job, "--json", "--non-interactive").ExitCode);
    }

    [Fact]
    public void Response_file_expansion_matches_direct_arguments()
    {
        var responsePath = Path.Combine(_directory, "args.rsp");
        File.WriteAllLines(responsePath, ["# comment", "scan", "a-rot90.jpg", "--json", "--non-interactive"]);

        var direct = Invoke("scan", "a-rot90.jpg", "--json", "--non-interactive");
        var expanded = Invoke("@" + responsePath);
        Assert.Equal(direct.ExitCode, expanded.ExitCode);
    }

    [Fact]
    public void Missing_response_file_is_invalid_argument()
        => Assert.Equal(
            (int)CliExitCode.InvalidArgument,
            Invoke("@" + Path.Combine(_directory, "absent.rsp")).ExitCode);

    [Fact]
    public void Simulated_cancellation_exits_40_with_cancelled_terminal_event()
    {
        var result = Invoke(
            "scan", "a-rot90.jpg", "b-rot90.jpg", "c-rot90.jpg",
            "--simulate-cancel-after", "1", "--jsonl", "--non-interactive");

        Assert.Equal((int)CliExitCode.Cancelled, result.ExitCode);
        var lines = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("\"result\":\"cancelled\"", lines[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Completion_script_is_deterministic_and_model_derived()
    {
        var first = Invoke("completion", "powershell");
        var second = Invoke("completion", "powershell");
        Assert.Equal(0, first.ExitCode);
        Assert.Equal(first.Stdout, second.Stdout);
        Assert.Contains("scan", first.Stdout, StringComparison.Ordinal);
        Assert.Contains("--acknowledge-in-place", first.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_reports_the_contract_version()
    {
        var result = Invoke("version", "--json");
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal(
            CliHost.ContractVersion,
            document.RootElement.GetProperty("contractVersion").GetString());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
