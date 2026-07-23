using System.Text.Json;
using OrientExpress.Cli.Hosting;
using OrientExpress.Contracts.Cli;

namespace OrientExpress.CliTests;

/// <summary>
/// Golden tests for `upright config init|show|validate` (PRD-FR-019,
/// AC-036/AC-037) with a hermetic HostEnvironment: no real user profile or
/// process environment is touched.
/// </summary>
public sealed class ConfigCommandTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-config-tests-").FullName;

    private HostEnvironment Environment(params (string Key, string Value)[] variables)
        => new(
            variables.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
            Path.Combine(_directory, "user", "config.json"),
            Path.Combine(_directory, "data"));

    private static (int ExitCode, string Stdout, string Stderr) Invoke(HostEnvironment environment, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliHost.Invoke(args, stdout, stderr, hostEnvironment: environment);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Validate_succeeds_on_defaults_alone()
    {
        var result = Invoke(Environment(), "config", "validate");
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        Assert.Contains("digest:", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_writes_a_valid_starter_and_refuses_silent_overwrite()
    {
        var environment = Environment();
        Assert.Equal((int)CliExitCode.Success, Invoke(environment, "config", "init").ExitCode);
        Assert.True(File.Exists(environment.UserConfigPath));
        Assert.Equal((int)CliExitCode.Success, Invoke(environment, "config", "validate").ExitCode);

        var refused = Invoke(environment, "config", "init");
        Assert.Equal((int)CliExitCode.Conflict, refused.ExitCode);
        Assert.Contains("CONFIG_EXISTS", refused.Stderr, StringComparison.Ordinal);
        Assert.Contains("--force", refused.Stderr, StringComparison.Ordinal);
        Assert.Equal((int)CliExitCode.Success, Invoke(environment, "config", "init", "--force").ExitCode);
    }

    [Fact]
    public void Unknown_fields_fail_before_processing_with_exit_21()
    {
        var environment = Environment();
        Directory.CreateDirectory(Path.GetDirectoryName(environment.UserConfigPath)!);
        File.WriteAllText(environment.UserConfigPath, """{"schemaVersion":"1.0","outpt":{"mode":"safe-copy"}}""");

        var result = Invoke(environment, "config", "validate");
        Assert.Equal((int)CliExitCode.InvalidConfiguration, result.ExitCode);
        Assert.Contains("CONFIG_PARSE_ERROR", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_values_fail_with_a_consumable_issues_document()
    {
        var environment = Environment(("UPRIGHT_OUTPUT__MODE", "overwrite"));
        var result = Invoke(environment, "config", "validate", "--json");
        Assert.Equal((int)CliExitCode.InvalidConfiguration, result.ExitCode);

        using var document = JsonDocument.Parse(result.Stdout);
        Assert.Equal("config.issues", document.RootElement.GetProperty("resultType").GetString());
        Assert.Equal(21, document.RootElement.GetProperty("exitCode").GetInt32());
        Assert.NotEmpty(document.RootElement.GetProperty("issues").EnumerateArray().ToList());
    }

    [Fact]
    public void Unknown_upright_environment_variables_fail_loudly()
    {
        var result = Invoke(Environment(("UPRIGHT_OUTPUT__MODES", "typo")), "config", "validate");
        Assert.Equal((int)CliExitCode.InvalidConfiguration, result.ExitCode);
        Assert.Contains("CONFIG_UNKNOWN_ENV_VARIABLE", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Precedence_cli_profile_beats_environment_beats_explicit_file()
    {
        var explicitPath = Path.Combine(_directory, "explicit.json");
        File.WriteAllText(explicitPath, """{"schemaVersion":"1.0","analysis":{"profile":"conservative"}}""");
        var environment = Environment(("UPRIGHT_ANALYSIS__PROFILE", "balanced"));

        var envWins = Invoke(environment, "config", "show", "--json", "--config", explicitPath);
        using (var document = JsonDocument.Parse(envWins.Stdout))
        {
            var effective = document.RootElement.GetProperty("effective");
            Assert.Equal(
                "balanced",
                effective.GetProperty("config").GetProperty("analysis").GetProperty("profile").GetString());
            Assert.Equal(
                "environment",
                effective.GetProperty("provenance").GetProperty("analysis.profile").GetString());
        }

        var cliWins = Invoke(environment, "config", "show", "--json", "--config", explicitPath, "--profile", "expert");
        using (var document = JsonDocument.Parse(cliWins.Stdout))
        {
            var effective = document.RootElement.GetProperty("effective");
            Assert.Equal(
                "expert",
                effective.GetProperty("config").GetProperty("analysis").GetProperty("profile").GetString());
            Assert.Equal(
                "cli",
                effective.GetProperty("provenance").GetProperty("analysis.profile").GetString());
        }
    }

    [Fact]
    public void Missing_explicit_config_file_is_a_configuration_error()
    {
        var result = Invoke(Environment(), "config", "validate", "--config", Path.Combine(_directory, "absent.json"));
        Assert.Equal((int)CliExitCode.InvalidConfiguration, result.ExitCode);
        Assert.Contains("CONFIG_FILE_NOT_FOUND", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Redacted_show_hides_path_values_but_keeps_the_digest()
    {
        var environment = Environment();
        var plain = Invoke(environment, "config", "show", "--json");
        var redacted = Invoke(environment, "config", "show", "--json", "--redacted");

        using var plainDocument = JsonDocument.Parse(plain.Stdout);
        using var redactedDocument = JsonDocument.Parse(redacted.Stdout);
        var plainJobRoot = plainDocument.RootElement.GetProperty("effective").GetProperty("config")
            .GetProperty("paths").GetProperty("jobRoot").GetString()!;
        var redactedJobRoot = redactedDocument.RootElement.GetProperty("effective").GetProperty("config")
            .GetProperty("paths").GetProperty("jobRoot").GetString()!;
        Assert.DoesNotContain(_directory, redacted.Stdout, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("redacted:", redactedJobRoot, StringComparison.Ordinal);
        Assert.NotEqual(plainJobRoot, redactedJobRoot);
        Assert.Equal(
            plainDocument.RootElement.GetProperty("effective").GetProperty("digest").GetString(),
            redactedDocument.RootElement.GetProperty("effective").GetProperty("digest").GetString());
    }

    [Fact]
    public void Telemetry_cannot_be_enabled_from_any_layer()
    {
        var result = Invoke(Environment(("UPRIGHT_PRIVACY__TELEMETRY", "true")), "config", "validate");
        Assert.Equal((int)CliExitCode.InvalidConfiguration, result.ExitCode);
        Assert.Contains("CONFIG_TELEMETRY_FORBIDDEN", result.Stderr, StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
