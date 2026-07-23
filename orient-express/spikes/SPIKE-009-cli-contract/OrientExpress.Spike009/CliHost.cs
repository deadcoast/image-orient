using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrientExpress.Spike009.Contract;
using OrientExpress.Spike009.Simulation;
using OrientExpress.Spike009.Support;

namespace OrientExpress.Spike009;

/// <summary>
/// Prototype of the DOC-CLI command contract on System.CommandLine (DEP-002):
/// representative command tree, global output options, machine streams,
/// destructive-flag guard, sealed-plan consumption, and the exit registry.
/// </summary>
public static class CliHost
{
    public const string ContractVersion = "cli-contract/1.0-draft";
    public const string ExecutableName = "upright-proto";

    private static readonly JsonSerializerOptions DocumentOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static int Invoke(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        try
        {
            return InvokeCore(args, stdout, stderr);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException)
        {
            stderr.WriteLine("internal error [INTERNAL_ERROR]: " + exception.Message);
            return (int)CliExitCode.InternalError;
        }
    }

    private static int InvokeCore(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (!ResponseFiles.TryExpand(args, out var expanded, out var expansionError))
        {
            stderr.WriteLine($"error [INVALID_ARGUMENT]: {expansionError}");
            return (int)CliExitCode.InvalidArgument;
        }

        var model = CommandModel.Build();
        var parse = model.Root.Parse(expanded);

        // --help/--version use the parser's own renderer on the real console.
        if (expanded.Any(token => token is "--help" or "-h" or "-?" or "--version"))
        {
            return parse.Invoke();
        }

        if (parse.Errors.Count > 0)
        {
            foreach (var parseError in parse.Errors)
            {
                stderr.WriteLine($"error [INVALID_ARGUMENT]: {parseError.Message}");
            }

            return (int)CliExitCode.InvalidArgument;
        }

        if (!TryResolveOutput(parse, model, stdout, stderr, out var context, out var contextError))
        {
            stderr.WriteLine($"error [INVALID_ARGUMENT]: {contextError}");
            return (int)CliExitCode.InvalidArgument;
        }

        var command = parse.CommandResult.Command;
        if (command == model.Version)
        {
            return RunVersion(context);
        }

        if (command == model.Doctor)
        {
            return RunDoctor(context);
        }

        if (command == model.Inspect)
        {
            return RunItems(context, "inspect", [parse.GetValue(model.InspectFile)!], jobDirectory: null, mode: "safe-copy", cancelAfter: 0);
        }

        if (command == model.Scan)
        {
            var mode = parse.GetValue(model.Mode) ?? "safe-copy";
            if (mode is not ("safe-copy" or "in-place"))
            {
                stderr.WriteLine($"error [INVALID_ARGUMENT]: --mode must be safe-copy or in-place, not '{mode}'");
                return (int)CliExitCode.InvalidArgument;
            }

            return RunItems(
                context,
                "scan",
                parse.GetValue(model.ScanPaths)!,
                parse.GetValue(model.JobDirectory),
                mode,
                parse.GetValue(model.CancelAfter));
        }

        if (command == model.Apply)
        {
            return RunApply(
                context,
                parse.GetValue(model.ApplyJob)!,
                parse.GetValue(model.AcknowledgeInPlace));
        }

        if (command == model.Completion)
        {
            var shell = parse.GetValue(model.CompletionShell)!;
            if (shell != "powershell")
            {
                stderr.WriteLine($"error [INVALID_ARGUMENT]: unsupported completion shell '{shell}'");
                return (int)CliExitCode.InvalidArgument;
            }

            context.Out.Write(PowerShellCompletion.Generate(model.Root));
            return (int)CliExitCode.Success;
        }

        stderr.WriteLine($"error [INVALID_ARGUMENT]: a command is required; see '{ExecutableName} --help'");
        return (int)CliExitCode.InvalidArgument;
    }

    private static bool TryResolveOutput(
        ParseResult parse,
        CommandModel model,
        TextWriter stdout,
        TextWriter stderr,
        out OutputContext context,
        out string? error)
    {
        var formatValue = parse.GetValue(model.OutputFormat) ?? "human";
        var json = parse.GetValue(model.JsonShortcut);
        var jsonl = parse.GetValue(model.JsonlShortcut);
        context = null!;
        if (json && jsonl)
        {
            error = "--json and --jsonl are mutually exclusive";
            return false;
        }

        var format = formatValue switch
        {
            "human" => OutputFormat.Human,
            "json" => OutputFormat.Json,
            "jsonl" => OutputFormat.Jsonl,
            _ => (OutputFormat)(-1),
        };
        if ((int)format == -1)
        {
            error = $"--output-format must be human, json, or jsonl, not '{formatValue}'";
            return false;
        }

        if (json)
        {
            format = OutputFormat.Json;
        }

        if (jsonl)
        {
            format = OutputFormat.Jsonl;
        }

        var verbosity = parse.GetValue(model.Verbosity) ?? "normal";
        if (verbosity is not ("quiet" or "normal" or "detailed" or "diagnostic"))
        {
            error = $"--verbosity value '{verbosity}' is not in the contract";
            return false;
        }

        context = new OutputContext(
            format,
            parse.GetValue(model.NoColor),
            verbosity,
            parse.GetValue(model.CorrelationId),
            parse.GetValue(model.NonInteractive),
            stdout,
            stderr);
        error = null;
        return true;
    }

    private static int RunVersion(OutputContext context)
    {
        if (context.Machine)
        {
            WriteDocument(context, new CommandResultDocument
            {
                Command = "version",
                ContractVersion = ContractVersion,
                ExitCode = 0,
                ExitSymbol = "SUCCESS",
                CorrelationId = context.CorrelationId,
            });
        }
        else
        {
            context.Out.WriteLine($"{ExecutableName} version — contract {ContractVersion}");
        }

        return (int)CliExitCode.Success;
    }

    private static int RunDoctor(OutputContext context)
    {
        var capabilities = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["format.jpeg"] = "guaranteed",
            ["format.png"] = "guaranteed",
            ["format.heic"] = "gated:SPIKE-007",
            ["mode.safe-copy"] = "available",
            ["mode.in-place"] = "gated:full-fault-suite",
            ["metadata.exiftool"] = "gated:SPIKE-004",
        };
        if (context.Machine)
        {
            WriteDocument(context, new CommandResultDocument
            {
                Command = "doctor",
                ContractVersion = ContractVersion,
                ExitCode = 0,
                ExitSymbol = "SUCCESS",
                CorrelationId = context.CorrelationId,
                Capabilities = capabilities,
            });
        }
        else
        {
            context.Out.WriteLine($"{ExecutableName} doctor — diagnostic mode, no files modified");
            foreach (var capability in capabilities)
            {
                context.Out.WriteLine($"  {capability.Key}: {capability.Value}");
            }
        }

        return (int)CliExitCode.Success;
    }

    private static int RunItems(
        OutputContext context,
        string command,
        string[] pathTokens,
        string? jobDirectory,
        string mode,
        int cancelAfter)
    {
        // SPIKE-009 finding: System.CommandLine binds unknown option-like
        // tokens into a greedy string[] argument instead of erroring, so the
        // contract's grammar guarantee needs this explicit host-side check.
        var optionLike = pathTokens.FirstOrDefault(token => token.StartsWith('-'));
        if (optionLike is not null)
        {
            context.Error.WriteLine($"error [INVALID_ARGUMENT]: unknown option '{optionLike}'");
            return (int)CliExitCode.InvalidArgument;
        }
        var rows = new List<PlanRow>();
        var cancelled = false;
        foreach (var (pathToken, index) in pathTokens.Select((token, index) => (token, index)))
        {
            if (cancelAfter > 0 && index >= cancelAfter)
            {
                cancelled = true;
                break;
            }

            rows.Add(SyntheticClassifier.Classify(pathToken));
            context.Progress($"{index + 1}/{pathTokens.Length} {pathToken}");
        }

        string? jobId = null;
        if (jobDirectory is not null && !cancelled)
        {
            jobId = PlanStore.Seal(jobDirectory, ContractVersion, mode, rows).JobId;
        }

        var factory = new EventFactory(command, jobId, context.CorrelationId);
        var exitCode = SyntheticClassifier.Aggregate(rows, cancelled);
        EmitItems(context, command, factory, rows, cancelled, mode, jobId, exitCode);
        return (int)exitCode;
    }

    private static int RunApply(OutputContext context, string jobDirectory, bool acknowledgeInPlace)
    {
        if (!PlanStore.TryLoad(jobDirectory, out var plan, out var digestValid) || plan is null)
        {
            context.Error.WriteLine($"error [INVALID_ARGUMENT]: no sealed plan at '{jobDirectory}'");
            return (int)CliExitCode.InvalidArgument;
        }

        if (!digestValid)
        {
            context.Error.WriteLine(
                "error [STALE_PLAN]: plan digest mismatch; re-run scan to seal a fresh plan");
            return (int)CliExitCode.StalePlan;
        }

        if (plan.Mode == "in-place" && !acknowledgeInPlace)
        {
            // The destructive guard: refused before any work happens.
            context.Error.WriteLine(
                "error [INVALID_ARGUMENT] [IN_PLACE_NOT_ACKNOWLEDGED]: " +
                "in-place plans require --acknowledge-in-place");
            return (int)CliExitCode.InvalidArgument;
        }

        var factory = new EventFactory("apply", plan.JobId, context.CorrelationId);
        var exitCode = SyntheticClassifier.Aggregate(plan.Rows, cancelled: false);
        EmitItems(context, "apply", factory, plan.Rows, cancelled: false, plan.Mode, plan.JobId, exitCode);
        return (int)exitCode;
    }

    private static void EmitItems(
        OutputContext context,
        string command,
        EventFactory factory,
        IReadOnlyList<PlanRow> rows,
        bool cancelled,
        string mode,
        string? jobId,
        CliExitCode exitCode)
    {
        var counts = SyntheticClassifier.Counts(rows);
        switch (context.Format)
        {
            case OutputFormat.Jsonl:
                foreach (var row in rows)
                {
                    context.Out.WriteLine(EventFactory.Serialize(factory.Create(
                        command + ".file",
                        row.Result,
                        row.Action,
                        row.PathToken,
                        row.ReasonCode is null ? null : [row.ReasonCode])));
                }

                context.Out.WriteLine(EventFactory.Serialize(factory.Create(
                    command + ".completed",
                    cancelled ? "cancelled" : "succeeded",
                    data: counts)));
                break;

            case OutputFormat.Json:
                WriteDocument(context, new CommandResultDocument
                {
                    Command = command,
                    ContractVersion = ContractVersion,
                    JobId = jobId,
                    Mode = mode,
                    ExitCode = (int)exitCode,
                    ExitSymbol = CliExitCodeRegistry.SymbolOf(exitCode),
                    CorrelationId = context.CorrelationId,
                    Cancelled = cancelled ? true : null,
                    Counts = counts,
                    Items = rows,
                });
                break;

            default:
                context.Out.WriteLine($"{ExecutableName} {command} — mode={mode} job={jobId ?? "none"}");
                foreach (var count in counts.Where(pair => pair.Value > 0))
                {
                    context.Out.WriteLine($"  {count.Key}: {count.Value}");
                }

                if (cancelled)
                {
                    context.Out.WriteLine("  cancelled: durable state retained; re-run to resume");
                }

                context.Out.WriteLine($"  exit: {(int)exitCode} {CliExitCodeRegistry.SymbolOf(exitCode)}");
                break;
        }
    }

    private static void WriteDocument(OutputContext context, CommandResultDocument document)
        => context.Out.WriteLine(JsonSerializer.Serialize(document, DocumentOptions));

    /// <summary>Single-document machine result ("command-result type" per DOC-CLI machine rules).</summary>
    private sealed record CommandResultDocument
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("resultType")]
        public string ResultType { get; init; } = "command.result";

        [JsonPropertyName("command")]
        public required string Command { get; init; }

        [JsonPropertyName("contractVersion")]
        public required string ContractVersion { get; init; }

        [JsonPropertyName("jobId")]
        public string? JobId { get; init; }

        [JsonPropertyName("mode")]
        public string? Mode { get; init; }

        [JsonPropertyName("exitCode")]
        public required int ExitCode { get; init; }

        [JsonPropertyName("exitSymbol")]
        public required string ExitSymbol { get; init; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; init; }

        [JsonPropertyName("cancelled")]
        public bool? Cancelled { get; init; }

        [JsonPropertyName("counts")]
        public IReadOnlyDictionary<string, int>? Counts { get; init; }

        [JsonPropertyName("capabilities")]
        public IReadOnlyDictionary<string, string>? Capabilities { get; init; }

        [JsonPropertyName("items")]
        public IReadOnlyList<PlanRow>? Items { get; init; }
    }
}
