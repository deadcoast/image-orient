using System.CommandLine;
using OrientExpress.Cli.Commands;
using OrientExpress.Cli.Config;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;

namespace OrientExpress.Cli.Hosting;

/// <summary>
/// The production `upright` host (WP-002). Implements the DOC-CLI contract
/// with the SPIKE-009 decisions: host-expanded response files, host-side
/// rejection of option-like positionals, grammar errors on stderr with empty
/// machine stdout, capability refusals as machine documents, and the frozen
/// exit registry.
/// </summary>
public static class CliHost
{
    public const string ContractVersion = "cli-contract/1.0";
    public const string ExecutableName = "upright";

    public static int Invoke(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        HostEnvironment? hostEnvironment = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        try
        {
            return InvokeCore(args, stdout, stderr, hostEnvironment ?? HostEnvironment.CreateDefault(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            stderr.WriteLine(
                $"error [{CliExitCodeRegistry.SymbolOf(CliExitCode.Cancelled)}] [OPERATION_CANCELLED]: " +
                "cancelled by request; next: re-run the command to continue");
            return (int)CliExitCode.Cancelled;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            stderr.WriteLine(
                $"error [{CliExitCodeRegistry.SymbolOf(CliExitCode.InternalError)}] [UNEXPECTED_FAILURE]: " +
                $"{exception.Message}; next: re-run with --verbosity diagnostic and report the issue");
            return (int)CliExitCode.InternalError;
        }
    }

    private static int InvokeCore(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        HostEnvironment hostEnvironment,
        CancellationToken cancellationToken)
    {
        if (!ResponseFiles.TryExpand(args, out var expanded, out var expansionError))
        {
            WriteGrammarError(stderr, expansionError!);
            return (int)CliExitCode.InvalidArgument;
        }

        var model = CommandLineModel.Build();
        var parse = model.Root.Parse(expanded);

        // --help/--version render through the parser's own writer on the console.
        if (expanded.Any(token => token is "--help" or "-h" or "-?" or "--version"))
        {
            return parse.Invoke();
        }

        if (parse.Errors.Count > 0)
        {
            foreach (var parseError in parse.Errors)
            {
                WriteGrammarError(stderr, parseError.Message);
            }

            return (int)CliExitCode.InvalidArgument;
        }

        if (!OutputContextResolver.TryResolve(parse, model, stdout, stderr, out var context, out var contextError))
        {
            WriteGrammarError(stderr, contextError!);
            return (int)CliExitCode.InvalidArgument;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var command = parse.CommandResult.Command;
        if (command == model.Version)
        {
            return VersionCommand.Run(context);
        }

        if (command == model.Doctor)
        {
            using var metadata = CreateMetadataService(hostEnvironment);
            return DoctorCommand.Run(context, parse.GetValue(model.DoctorProbeTarget), metadata, cancellationToken);
        }

        if (command == model.Inspect)
        {
            using var metadata = CreateMetadataService(hostEnvironment);
            return InspectCommand.Run(
                context,
                parse.GetValue(model.InspectFile)!,
                metadata,
                new OrientExpress.Infrastructure.Imaging.ManagedFormatProber(),
                cancellationToken);
        }

        if (command == model.Completion)
        {
            return CompletionCommand.Run(context, model, parse.GetValue(model.CompletionShell)!);
        }

        if (command == model.ConfigInit)
        {
            return ConfigCommands.Init(
                context,
                hostEnvironment,
                parse.GetValue(model.ConfigInitPath),
                parse.GetValue(model.ConfigInitForce));
        }

        if (command == model.ConfigShow || command == model.ConfigValidate)
        {
            var load = ConfigLoader.Load(
                hostEnvironment,
                parse.GetValue(model.ConfigPath),
                parse.GetValue(model.Profile));
            return command == model.ConfigShow
                ? ConfigCommands.Show(context, load, parse.GetValue(model.ConfigShowRedacted))
                : ConfigCommands.Validate(context, load);
        }

        if (model.Gated.TryGetValue(command, out var spec))
        {
            return RefuseGated(context, spec);
        }

        // Bare root or a pure namespace command: grammar guidance, empty machine stdout.
        WriteGrammarError(stderr, $"a command is required; see '{ExecutableName} --help'");
        return (int)CliExitCode.InvalidArgument;
    }

    /// <summary>
    /// Capability refusal is a *valid* command outcome, so unlike grammar
    /// errors it produces a machine document callers can consume (DOC-CLI:
    /// stable reason code, safe message, affected item, next action).
    /// </summary>
    private static int RefuseGated(OutputContext context, GatedCommandSpec spec)
    {
        const string ReasonCode = "NOT_YET_IMPLEMENTED";
        var message = $"'{spec.Path}' is gated until {spec.RequiredWork} evidence exists";
        var nextAction = "see STATUS.md gates; run 'upright doctor' for current capabilities";
        if (context.Machine)
        {
            if (context.Format == OutputFormat.Jsonl)
            {
                MachineWriter.WriteEventLine(context, MachineWriter.NewEvent(
                    context, spec.Path, "command.refused", "unsupported", [ReasonCode]));
            }
            else
            {
                MachineWriter.WriteDocument(context, new CommandResultDocument
                {
                    Command = spec.Path,
                    ContractVersion = ContractVersion,
                    ExitCode = (int)CliExitCode.CapabilityUnavailable,
                    ExitSymbol = CliExitCodeRegistry.SymbolOf(CliExitCode.CapabilityUnavailable),
                    CorrelationId = context.CorrelationId,
                    ReasonCodes = [ReasonCode],
                    Message = message,
                    NextAction = nextAction,
                });
            }
        }

        context.WriteError(CliExitCode.CapabilityUnavailable, ReasonCode, message, nextAction);
        return (int)CliExitCode.CapabilityUnavailable;
    }

    private static OrientExpress.Infrastructure.Metadata.ExifTool.ExifToolMetadataService CreateMetadataService(
        HostEnvironment hostEnvironment)
        => new OrientExpress.Infrastructure.Metadata.ExifTool.ExifToolMetadataService(
            OrientExpress.Infrastructure.Metadata.ExifTool.ExifToolLocator.Locate(
                hostEnvironment.EnvironmentVariables));

    private static void WriteGrammarError(TextWriter stderr, string message)
        => stderr.WriteLine(
            $"error [{CliExitCodeRegistry.SymbolOf(CliExitCode.InvalidArgument)}] [CLI_GRAMMAR]: " +
            $"{message}; next: see '{ExecutableName} --help'");
}
