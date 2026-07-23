using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrientExpress.Application.Config;
using OrientExpress.Cli.Config;
using OrientExpress.Cli.Hosting;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Config;

namespace OrientExpress.Cli.Commands;

/// <summary>`upright config init|show|validate` (PRD-FR-019, WP-003).</summary>
public static class ConfigCommands
{
    public static int Init(OutputContext context, HostEnvironment environment, string? path, bool force)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(environment);
        var target = path ?? environment.UserConfigPath;
        if (File.Exists(target) && !force)
        {
            context.WriteError(
                CliExitCode.Conflict,
                "CONFIG_EXISTS",
                $"a configuration already exists at '{target}'",
                "re-run with --force to overwrite, or edit the existing file");
            return (int)CliExitCode.Conflict;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(target));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var starter = ConfigDefaults.Create(environment.DataRoot);
        File.WriteAllText(
            target,
            JsonSerializer.Serialize(starter, StarterOptions) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new CommandResultDocument
            {
                Command = "config init",
                ContractVersion = CliHost.ContractVersion,
                ExitCode = (int)CliExitCode.Success,
                ExitSymbol = CliExitCodeRegistry.SymbolOf(CliExitCode.Success),
                CorrelationId = context.CorrelationId,
                Message = $"starter configuration written to {target}",
            });
        }
        else
        {
            context.Out.WriteLine($"{CliHost.ExecutableName} config init — wrote starter configuration");
            context.Out.WriteLine($"  path: {target}");
            context.Out.WriteLine("  edit it, then run 'upright config validate'");
        }

        return (int)CliExitCode.Success;
    }

    public static int Show(OutputContext context, ConfigLoadResult load, bool redacted)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(load);
        if (load.Effective is null)
        {
            return ReportIssues(context, "config show", load.Issues);
        }

        var effective = redacted ? Redact(load.Effective) : load.Effective;
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new EffectiveConfigDocument
            {
                Command = "config show",
                ContractVersion = CliHost.ContractVersion,
                CorrelationId = context.CorrelationId,
                Redacted = redacted,
                Effective = effective,
            });
        }
        else
        {
            context.Out.WriteLine($"{CliHost.ExecutableName} config show — effective configuration");
            context.Out.WriteLine($"  digest: {effective.Digest}");
            var bySource = effective.Provenance.Values
                .GroupBy(source => source, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal);
            foreach (var group in bySource)
            {
                context.Out.WriteLine($"  from {group.Key}: {group.Count()} settings");
            }

            if (!context.Quiet)
            {
                foreach (var (settingPath, source) in effective.Provenance.Where(pair => pair.Value != ConfigLayer.Defaults))
                {
                    context.Out.WriteLine($"    {settingPath} <- {source}");
                }
            }
        }

        return (int)CliExitCode.Success;
    }

    public static int Validate(OutputContext context, ConfigLoadResult load)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(load);
        if (load.Effective is null)
        {
            return ReportIssues(context, "config validate", load.Issues);
        }

        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new CommandResultDocument
            {
                Command = "config validate",
                ContractVersion = CliHost.ContractVersion,
                ExitCode = (int)CliExitCode.Success,
                ExitSymbol = CliExitCodeRegistry.SymbolOf(CliExitCode.Success),
                CorrelationId = context.CorrelationId,
                Message = "configuration is valid; digest " + load.Effective.Digest,
            });
        }
        else
        {
            context.Out.WriteLine($"{CliHost.ExecutableName} config validate — configuration is valid");
            context.Out.WriteLine($"  digest: {load.Effective.Digest}");
        }

        return (int)CliExitCode.Success;
    }

    /// <summary>
    /// Invalid configuration: exit 21 with a consumable issues document on
    /// machine stdout (the report is the command's product) and stable error
    /// lines on stderr (AC-037).
    /// </summary>
    private static int ReportIssues(OutputContext context, string command, IReadOnlyList<ConfigIssue> issues)
    {
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new ConfigIssuesDocument
            {
                Command = command,
                ContractVersion = CliHost.ContractVersion,
                CorrelationId = context.CorrelationId,
                Issues = issues,
            });
        }

        foreach (var issue in issues)
        {
            context.WriteError(
                CliExitCode.InvalidConfiguration,
                issue.Code,
                $"{issue.Path}: {issue.Message}",
                "fix the configuration and re-run 'upright config validate'");
        }

        return (int)CliExitCode.InvalidConfiguration;
    }

    private static EffectiveConfig Redact(EffectiveConfig effective)
    {
        var paths = effective.Config.Paths;
        if (paths is null)
        {
            return effective;
        }

        return effective with
        {
            Config = effective.Config with
            {
                Paths = new PathsConfig
                {
                    JobRoot = RedactValue(paths.JobRoot),
                    ModelRoot = RedactValue(paths.ModelRoot),
                    CacheRoot = RedactValue(paths.CacheRoot),
                    BackupRoot = RedactValue(paths.BackupRoot),
                },
            },
        };
    }

    private static string? RedactValue(string? value)
        => value is null
            ? null
            : "redacted:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8];

    private static readonly JsonSerializerOptions StarterOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private sealed record EffectiveConfigDocument
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("resultType")]
        public string ResultType { get; init; } = "config.effective";

        [JsonPropertyName("command")]
        public required string Command { get; init; }

        [JsonPropertyName("contractVersion")]
        public required string ContractVersion { get; init; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; init; }

        [JsonPropertyName("redacted")]
        public required bool Redacted { get; init; }

        [JsonPropertyName("effective")]
        public required EffectiveConfig Effective { get; init; }
    }

    private sealed record ConfigIssuesDocument
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("resultType")]
        public string ResultType { get; init; } = "config.issues";

        [JsonPropertyName("command")]
        public required string Command { get; init; }

        [JsonPropertyName("contractVersion")]
        public required string ContractVersion { get; init; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; init; }

        [JsonPropertyName("exitCode")]
        public int ExitCode { get; init; } = (int)CliExitCode.InvalidConfiguration;

        [JsonPropertyName("exitSymbol")]
        public string ExitSymbol { get; init; } = CliExitCodeRegistry.SymbolOf(CliExitCode.InvalidConfiguration);

        [JsonPropertyName("issues")]
        public required IReadOnlyList<ConfigIssue> Issues { get; init; }
    }
}
