using System.Text.Json;
using OrientExpress.Cli.Hosting;
using OrientExpress.Contracts.Cli;

namespace OrientExpress.CliTests;

/// <summary>
/// Golden tests for the DOC-CLI contract (TEST anchor for AC-003, AC-033,
/// AC-034, AC-035 in-process portion, AC-048). Byte-level encoding evidence
/// is covered by the SPIKE-009 out-of-process harness.
/// </summary>
public sealed class CliHostContractTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-cli-tests-").FullName;

    private static (int ExitCode, string Stdout, string Stderr) Invoke(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliHost.Invoke(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Version_human_names_tool_and_contract()
    {
        var result = Invoke("version");
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("upright ", result.Stdout, StringComparison.Ordinal);
        Assert.Contains(CliHost.ContractVersion, result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Version_json_is_one_schema_shaped_document()
    {
        var result = Invoke("version", "--json");
        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal("command.result", document.RootElement.GetProperty("resultType").GetString());
        Assert.Equal(CliHost.ContractVersion, document.RootElement.GetProperty("contractVersion").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("exitCode").GetInt32());
    }

    [Fact]
    public void Doctor_json_emits_a_capability_report_conforming_to_the_schema_shape()
    {
        var result = Invoke("doctor", "--json", "--non-interactive");
        Assert.Equal(0, result.ExitCode);

        using var document = JsonDocument.Parse(result.Stdout);
        var root = document.RootElement;
        foreach (var required in new[] { "schemaVersion", "generatedUtc", "tool", "platform", "components", "formats" })
        {
            Assert.True(root.TryGetProperty(required, out _), $"missing {required}");
        }

        Assert.Equal("1.0", root.GetProperty("schemaVersion").GetString());
        foreach (var component in root.GetProperty("components").EnumerateArray())
        {
            Assert.True(component.TryGetProperty("id", out _));
            Assert.True(component.TryGetProperty("available", out _));
            Assert.True(component.TryGetProperty("status", out _));
        }

        foreach (var format in root.GetProperty("formats").EnumerateArray())
        {
            foreach (var required in new[] { "format", "read", "metadataRead", "transform", "metadataWrite", "verify" })
            {
                Assert.True(format.TryGetProperty(required, out _), $"missing format field {required}");
            }
        }
    }

    [Fact]
    public void Doctor_capability_truth_telling_matches_implemented_work()
    {
        var result = Invoke("doctor", "--json");
        using var document = JsonDocument.Parse(result.Stdout);
        var availability = document.RootElement.GetProperty("components").EnumerateArray()
            .ToDictionary(
                component => component.GetProperty("id").GetString()!,
                component => component.GetProperty("available").GetBoolean());
        string[] implemented = ["cli-host", "configuration", "filesystem-identity", "format-prober", "inference-runtime", "job-store"];
        string[] machineDependent = ["metadata-exiftool", "orientation-model", "transform-jpegtran"];
        Assert.All(implemented, id => Assert.True(availability[id], id));
        Assert.All(
            availability.Where(pair => !implemented.Contains(pair.Key) && !machineDependent.Contains(pair.Key)),
            pair => Assert.False(pair.Value, pair.Key));
    }

    [Fact]
    public void Doctor_probe_target_runs_filesystem_probes_and_cleans_up()
    {
        var probeDirectory = Path.Combine(_directory, "probe");
        Directory.CreateDirectory(probeDirectory);

        var result = Invoke("doctor", "--probe-target", probeDirectory, "--json", "--non-interactive");
        Assert.Equal(0, result.ExitCode);

        using var document = JsonDocument.Parse(result.Stdout);
        var probes = document.RootElement.GetProperty("components").EnumerateArray()
            .Where(component => component.GetProperty("id").GetString()!.StartsWith("probe.", StringComparison.Ordinal))
            .ToDictionary(
                component => component.GetProperty("id").GetString()!,
                component => component.GetProperty("available").GetBoolean());
        foreach (var id in new[] { "probe.volume", "probe.identity", "probe.atomic-replace", "probe.create-path" })
        {
            Assert.True(probes[id], id);
        }

        Assert.Empty(Directory.GetFiles(probeDirectory));
    }

    [Fact]
    public void Doctor_probe_target_refuses_a_missing_directory()
    {
        var result = Invoke("doctor", "--probe-target", Path.Combine(_directory, "absent"));
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Contains("PROBE_TARGET_NOT_FOUND", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Doctor_jsonl_lines_each_parse_with_required_event_fields()
    {
        var result = Invoke("doctor", "--jsonl", "--non-interactive");
        var lines = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2);
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
    public void Machine_stdout_carries_no_prose_or_ansi()
    {
        var result = Invoke("doctor", "--jsonl", "--non-interactive");
        Assert.DoesNotContain('\u001b', result.Stdout);
        Assert.All(
            result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            line => Assert.StartsWith("{", line.TrimEnd('\r'), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("scan")]
    [InlineData("apply")]
    [InlineData("run")]
    [InlineData("undo")]
    [InlineData("review", "decide")]
    [InlineData("jobs", "prune")]
    [InlineData("review", "export")]
    [InlineData("verify")]
    [InlineData("benchmark")]
    public void Gated_commands_refuse_with_capability_unavailable(params string[] command)
    {
        var result = Invoke(command);
        Assert.Equal((int)CliExitCode.CapabilityUnavailable, result.ExitCode);
        Assert.Contains("NOT_YET_IMPLEMENTED", result.Stderr, StringComparison.Ordinal);
        Assert.Contains("next:", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Gated_commands_accept_arguments_and_still_refuse_honestly()
    {
        var result = Invoke("scan", "C:/photos", "--json");
        Assert.Equal((int)CliExitCode.CapabilityUnavailable, result.ExitCode);

        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal(22, document.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal("CAPABILITY_UNAVAILABLE", document.RootElement.GetProperty("exitSymbol").GetString());
        Assert.Contains(
            "NOT_YET_IMPLEMENTED",
            document.RootElement.GetProperty("reasonCodes").EnumerateArray().Select(reason => reason.GetString()));
    }

    [Fact]
    public void Grammar_errors_keep_machine_stdout_empty()
    {
        var result = Invoke("doctor", "--wat", "--json");
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Equal(string.Empty, result.Stdout);
        Assert.Contains("CLI_GRAMMAR", result.Stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--json", "--jsonl")]
    [InlineData("--output-format", "xml")]
    [InlineData("--verbosity", "loud")]
    public void Invalid_global_values_are_grammar_errors(params string[] extra)
        => Assert.Equal((int)CliExitCode.InvalidArgument, Invoke(["version", .. extra]).ExitCode);

    [Fact]
    public void Overlong_correlation_id_is_rejected()
        => Assert.Equal(
            (int)CliExitCode.InvalidArgument,
            Invoke("version", "--correlation-id", new string('x', 129)).ExitCode);

    [Fact]
    public void Correlation_id_is_echoed_on_machine_events()
    {
        var result = Invoke("doctor", "--jsonl", "--correlation-id", "trace-42");
        Assert.All(
            result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            line => Assert.Contains("\"correlationId\":\"trace-42\"", line, StringComparison.Ordinal));
    }

    [Fact]
    public void Bare_invocation_is_a_grammar_error_with_guidance()
    {
        var result = Invoke();
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Contains("--help", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Response_files_expand_to_the_same_contract()
    {
        var responsePath = Path.Combine(_directory, "args.rsp");
        File.WriteAllLines(responsePath, ["# doctor via response file", "doctor", "--json", "--non-interactive"]);

        var direct = Invoke("doctor", "--json", "--non-interactive");
        var expanded = Invoke("@" + responsePath);
        Assert.Equal(direct.ExitCode, expanded.ExitCode);
        Assert.Equal((int)CliExitCode.Success, expanded.ExitCode);
    }

    [Fact]
    public void Missing_response_file_is_a_grammar_error()
        => Assert.Equal(
            (int)CliExitCode.InvalidArgument,
            Invoke("@" + Path.Combine(_directory, "absent.rsp")).ExitCode);

    [Fact]
    public void Completion_is_deterministic_and_covers_the_documented_tree()
    {
        var first = Invoke("completion", "powershell");
        var second = Invoke("completion", "powershell");
        Assert.Equal(0, first.ExitCode);
        Assert.Equal(first.Stdout, second.Stdout);
        foreach (var token in new[] { "doctor", "scan", "apply", "review", "models", "config", "--output-format", "--non-interactive" })
        {
            Assert.Contains(token, first.Stdout, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Unsupported_completion_shell_is_a_grammar_error()
        => Assert.Equal((int)CliExitCode.InvalidArgument, Invoke("completion", "bash").ExitCode);

    [Fact]
    public void Pre_cancelled_invocation_exits_with_the_cancelled_registry_code()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exitCode = CliHost.Invoke(["doctor"], stdout, stderr, cancellationToken: cancellation.Token);
        Assert.Equal((int)CliExitCode.Cancelled, exitCode);
        Assert.Contains("OPERATION_CANCELLED", stderr.ToString(), StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
