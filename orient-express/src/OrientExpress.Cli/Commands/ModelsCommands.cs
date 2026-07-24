using System.Text.Json;
using System.Text.Json.Serialization;
using OrientExpress.Application.Inference;
using OrientExpress.Cli.Hosting;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Models;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `upright models list|install|verify|activate|rollback|remove`
/// (PRD-FR-020, WP-006). Bundles are digest-pinned local files; nothing here
/// touches the network (NFR-PRIV-001).
/// </summary>
public static class ModelsCommands
{
    public static int List(OutputContext context, IModelRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);
        var models = registry.List();
        if (!models.IsSuccess)
        {
            return Report(context, "models list", models.Error!);
        }

        var items = models.Require();
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new ModelsListDocument
            {
                Command = "models list",
                ContractVersion = CliHost.ContractVersion,
                CorrelationId = context.CorrelationId,
                Models = [.. items.Select(ModelListItem.From)],
            });
        }
        else
        {
            context.Out.WriteLine($"{CliHost.ExecutableName} models list — {items.Count} installed");
            foreach (var model in items)
            {
                context.Out.WriteLine(
                    $"  {(model.IsActive ? "*" : " ")} {model.Manifest.ModelId} {model.Manifest.Version} "
                    + $"({model.Manifest.Task}; sha256 {model.Manifest.Artifact!.Sha256![..16]}…)");
            }

            if (items.Count == 0)
            {
                context.Out.WriteLine("  none; install one with 'upright models install <artifact> --manifest <file>'");
            }
        }

        return (int)CliExitCode.Success;
    }

    public static int Install(OutputContext context, IModelRegistry registry, string artifactPath, string manifestPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);
        if (!File.Exists(manifestPath))
        {
            context.WriteError(
                CliExitCode.InvalidArgument, "MODEL_MANIFEST_NOT_FOUND",
                $"no manifest at '{manifestPath}'", "pass --manifest <manifest.json>");
            return (int)CliExitCode.InvalidArgument;
        }

        ModelManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(manifestPath));
        }
        catch (JsonException exception)
        {
            context.WriteError(
                CliExitCode.IncompatibleContract, "MODEL_MANIFEST_INVALID",
                exception.Message, "fix the manifest JSON");
            return (int)CliExitCode.IncompatibleContract;
        }

        var installed = registry.Install(artifactPath, manifest!);
        if (!installed.IsSuccess)
        {
            return Report(context, "models install", installed.Error!);
        }

        return Success(
            context, "models install",
            $"installed {installed.Require().Manifest.ModelId} {installed.Require().Manifest.Version} (verified)");
    }

    public static int Verify(OutputContext context, IModelRegistry registry, string? modelId, string? version)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);
        if (modelId is not null && version is not null)
        {
            var single = registry.Verify(modelId, version);
            return single.IsSuccess
                ? Success(context, "models verify", $"{modelId} {version} verified")
                : Report(context, "models verify", single.Error!);
        }

        var all = registry.List();
        if (!all.IsSuccess)
        {
            return Report(context, "models verify", all.Error!);
        }

        var failures = new List<string>();
        foreach (var model in all.Require())
        {
            var check = registry.Verify(model.Manifest.ModelId!, model.Manifest.Version!);
            if (!check.IsSuccess)
            {
                failures.Add($"{model.Manifest.ModelId} {model.Manifest.Version}: {check.Error!.Code}");
            }
        }

        if (failures.Count > 0)
        {
            return Report(context, "models verify", new ErrorInfo(
                "MODEL_DIGEST_MISMATCH", ErrorCategory.IntegrityViolation, false,
                string.Join("; ", failures)));
        }

        return Success(context, "models verify", $"{all.Require().Count} model(s) verified");
    }

    public static int Activate(OutputContext context, IModelRegistry registry, string modelId, string version)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);
        var activated = registry.Activate(modelId, version);
        return activated.IsSuccess
            ? Success(context, "models activate", $"{modelId} {version} is now active")
            : Report(context, "models activate", activated.Error!);
    }

    public static int Rollback(OutputContext context, IModelRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);
        var restored = registry.Rollback();
        return restored.IsSuccess
            ? Success(
                context, "models rollback",
                $"restored {restored.Require().Manifest.ModelId} {restored.Require().Manifest.Version}")
            : Report(context, "models rollback", restored.Error!);
    }

    public static int Remove(OutputContext context, IModelRegistry registry, string modelId, string version)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(registry);
        var removed = registry.Remove(modelId, version);
        return removed.IsSuccess
            ? Success(context, "models remove", $"removed {modelId} {version}")
            : Report(context, "models remove", removed.Error!);
    }

    /// <summary>Stable code → registry exit-code mapping (frozen contract).</summary>
    internal static CliExitCode ExitCodeFor(ErrorInfo error)
        => error.Code switch
        {
            "MODEL_DIGEST_MISMATCH" => CliExitCode.VerificationFailed,
            "MODEL_MANIFEST_INVALID" => CliExitCode.IncompatibleContract,
            "MODEL_ACTIVE" or "MODEL_ALREADY_INSTALLED" => CliExitCode.Conflict,
            "MODEL_NOT_INSTALLED" or "MODEL_ARTIFACT_NOT_FOUND" or "MODEL_NO_ROLLBACK_TARGET"
                => CliExitCode.InvalidArgument,
            _ => CliExitCode.InternalError,
        };

    private static int Report(OutputContext context, string command, ErrorInfo error)
    {
        var exitCode = ExitCodeFor(error);
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new CommandResultDocument
            {
                Command = command,
                ContractVersion = CliHost.ContractVersion,
                ExitCode = (int)exitCode,
                ExitSymbol = CliExitCodeRegistry.SymbolOf(exitCode),
                CorrelationId = context.CorrelationId,
                ReasonCodes = [error.Code],
                Message = error.SafeMessage,
                NextAction = "run 'upright models list' to see installed models",
            });
        }

        context.WriteError(exitCode, error.Code, error.SafeMessage, "run 'upright models list' to inspect state");
        return (int)exitCode;
    }

    private static int Success(OutputContext context, string command, string message)
    {
        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, new CommandResultDocument
            {
                Command = command,
                ContractVersion = CliHost.ContractVersion,
                ExitCode = (int)CliExitCode.Success,
                ExitSymbol = CliExitCodeRegistry.SymbolOf(CliExitCode.Success),
                CorrelationId = context.CorrelationId,
                Message = message,
            });
        }
        else
        {
            context.Out.WriteLine($"{CliHost.ExecutableName} {command} — {message}");
        }

        return (int)CliExitCode.Success;
    }

    private sealed record ModelListItem
    {
        [JsonPropertyName("modelId")]
        public required string ModelId { get; init; }

        [JsonPropertyName("version")]
        public required string Version { get; init; }

        [JsonPropertyName("task")]
        public required string Task { get; init; }

        [JsonPropertyName("sha256")]
        public required string Sha256 { get; init; }

        [JsonPropertyName("active")]
        public required bool Active { get; init; }

        public static ModelListItem From(InstalledModel model)
            => new()
            {
                ModelId = model.Manifest.ModelId!,
                Version = model.Manifest.Version!,
                Task = model.Manifest.Task!,
                Sha256 = model.Manifest.Artifact!.Sha256!,
                Active = model.IsActive,
            };
    }

    private sealed record ModelsListDocument
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("resultType")]
        public string ResultType { get; init; } = "models.list";

        [JsonPropertyName("command")]
        public required string Command { get; init; }

        [JsonPropertyName("contractVersion")]
        public required string ContractVersion { get; init; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; init; }

        [JsonPropertyName("models")]
        public required IReadOnlyList<ModelListItem> Models { get; init; }
    }
}
