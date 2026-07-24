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
            var doctorConfig = ConfigLoader.Load(
                hostEnvironment, parse.GetValue(model.ConfigPath), parse.GetValue(model.Profile));
            var doctorRegistry = doctorConfig.Effective is null
                ? null
                : new OrientExpress.Infrastructure.Inference.FileModelRegistry(
                    doctorConfig.Effective.Config.Paths!.ModelRoot!);
            return DoctorCommand.Run(
                context,
                parse.GetValue(model.DoctorProbeTarget),
                metadata,
                doctorRegistry,
                hostEnvironment.EnvironmentVariables,
                cancellationToken);
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

        if (command == model.ModelsList || command == model.ModelsInstall || command == model.ModelsVerify
            || command == model.ModelsActivate || command == model.ModelsRollback || command == model.ModelsRemove)
        {
            var load = ConfigLoader.Load(
                hostEnvironment, parse.GetValue(model.ConfigPath), parse.GetValue(model.Profile));
            if (load.Effective is null)
            {
                foreach (var issue in load.Issues)
                {
                    context.WriteError(
                        CliExitCode.InvalidConfiguration, issue.Code, $"{issue.Path}: {issue.Message}",
                        "fix the configuration and re-run 'upright config validate'");
                }

                return (int)CliExitCode.InvalidConfiguration;
            }

            var registry = new OrientExpress.Infrastructure.Inference.FileModelRegistry(
                load.Effective.Config.Paths!.ModelRoot!);
            if (command == model.ModelsList)
            {
                return ModelsCommands.List(context, registry);
            }

            if (command == model.ModelsInstall)
            {
                return ModelsCommands.Install(
                    context, registry,
                    parse.GetValue(model.ModelsInstallArtifact)!,
                    parse.GetValue(model.ModelsInstallManifest)!);
            }

            if (command == model.ModelsVerify)
            {
                return ModelsCommands.Verify(
                    context, registry,
                    parse.GetValue(model.ModelsVerifyId),
                    parse.GetValue(model.ModelsVerifyVersion));
            }

            if (command == model.ModelsActivate)
            {
                return ModelsCommands.Activate(
                    context, registry,
                    parse.GetValue(model.ModelsActivateId)!,
                    parse.GetValue(model.ModelsActivateVersion)!);
            }

            if (command == model.ModelsRollback)
            {
                return ModelsCommands.Rollback(context, registry);
            }

            return ModelsCommands.Remove(
                context, registry,
                parse.GetValue(model.ModelsRemoveId)!,
                parse.GetValue(model.ModelsRemoveVersion)!);
        }

        if (command == model.Scan || command == model.Apply || command == model.Verify
            || command == model.Undo || command == model.JobsList || command == model.JobsShow
            || command == model.JobsIntegrity || command == model.JobsPrune)
        {
            var load = ConfigLoader.Load(
                hostEnvironment, parse.GetValue(model.ConfigPath), parse.GetValue(model.Profile));
            if (load.Effective is null)
            {
                foreach (var issue in load.Issues)
                {
                    context.WriteError(
                        CliExitCode.InvalidConfiguration, issue.Code, $"{issue.Path}: {issue.Message}",
                        "fix the configuration and re-run 'upright config validate'");
                }

                return (int)CliExitCode.InvalidConfiguration;
            }

            var jobStore = new OrientExpress.Infrastructure.Sqlite.SqliteJobStore(
                load.Effective.Config.Paths!.JobRoot!);
            try
            {
                if (command == model.JobsList)
                {
                    return JobsCommands.ListAsync(context, jobStore, cancellationToken).GetAwaiter().GetResult();
                }

                if (command == model.JobsShow)
                {
                    return JobsCommands.ShowAsync(
                        context, jobStore, parse.GetValue(model.JobsShowJob)!, cancellationToken)
                        .GetAwaiter().GetResult();
                }

                if (command == model.JobsIntegrity)
                {
                    return JobsCommands.IntegrityAsync(
                        context, jobStore, parse.GetValue(model.JobsIntegrityJob), cancellationToken)
                        .GetAwaiter().GetResult();
                }

                if (command == model.JobsPrune)
                {
                    return JobsCommands.PruneAsync(
                        context, jobStore, parse.GetValue(model.JobsPruneJob)!,
                        parse.GetValue(model.JobsPruneYes), cancellationToken)
                        .GetAwaiter().GetResult();
                }

                if (command == model.Undo)
                {
                    return UndoCommand.RunAsync(
                        context,
                        jobStore,
                        new OrientExpress.Infrastructure.Windows.WindowsAtomicFileOperations(),
                        parse.GetValue(model.UndoJob)!,
                        parse.GetValue(model.UndoYes),
                        cancellationToken).GetAwaiter().GetResult();
                }

                if (command == model.Verify)
                {
                    return VerifyCommand.RunAsync(
                        context,
                        jobStore,
                        new OrientExpress.Infrastructure.Imaging.ProbeOutputVerifier(
                            new OrientExpress.Infrastructure.Imaging.ManagedFormatProber()),
                        parse.GetValue(model.VerifyJob)!,
                        cancellationToken).GetAwaiter().GetResult();
                }

                var identity = new OrientExpress.Infrastructure.Windows.WindowsFileIdentityService();
                if (command == model.Scan)
                {
                    OrientExpress.Application.Orientation.FourCandidateOrientationEngine? mlEngine = null;
                    string? modelIdentity = null;
                    OrientExpress.Infrastructure.Inference.OnnxInferenceRuntime? mlRuntime = null;
                    try
                    {
                        if (parse.GetValue(model.ScanDetect))
                        {
                            var registry = new OrientExpress.Infrastructure.Inference.FileModelRegistry(
                                load.Effective.Config.Paths!.ModelRoot!);
                            var active = registry.GetActive();
                            if (!active.IsSuccess || active.Value is null)
                            {
                                context.WriteError(
                                    CliExitCode.CapabilityUnavailable, "NO_ACTIVE_MODEL",
                                    "--detect needs an active face model",
                                    "install and activate one via 'upright models' (see models/manifests)");
                                return (int)CliExitCode.CapabilityUnavailable;
                            }

                            var scanJpegtran = OrientExpress.Infrastructure.Imaging.JpegtranLocator.Locate(
                                hostEnvironment.EnvironmentVariables);
                            var djpegPath = scanJpegtran is null
                                ? null
                                : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(scanJpegtran)!, "djpeg.exe");
                            if (djpegPath is null || !File.Exists(djpegPath))
                            {
                                context.WriteError(
                                    CliExitCode.CapabilityUnavailable, "DJPEG_NOT_FOUND",
                                    "--detect needs the libjpeg-turbo decoder", "install libjpeg-turbo (DEP-008)");
                                return (int)CliExitCode.CapabilityUnavailable;
                            }

                            var installed = active.Value;
                            var manifest = installed.Manifest;
                            var handle = new OrientExpress.Contracts.Models.ModelHandle(
                                manifest.ModelId!, manifest.Version!, manifest.Artifact!.Sha256!,
                                installed.ArtifactPath);
                            mlRuntime = new OrientExpress.Infrastructure.Inference.OnnxInferenceRuntime();
                            modelIdentity =
                                $"{manifest.ModelId} {manifest.Version} sha256:{manifest.Artifact.Sha256![..16]}";
                            mlEngine = new OrientExpress.Application.Orientation.FourCandidateOrientationEngine(
                                new OrientExpress.Infrastructure.Imaging.DjpegPixelDecoder(djpegPath),
                                new OrientExpress.Infrastructure.Inference.YuNetFaceDetector(mlRuntime, handle),
                                modelIdentity,
                                $"{mlRuntime.Descriptor.Runtime} {mlRuntime.Descriptor.Version} {mlRuntime.Descriptor.Provider}");
                        }

                        return ScanCommand.RunAsync(
                            context,
                            load,
                            identity,
                            new OrientExpress.Infrastructure.Imaging.ManagedFormatProber(),
                            jobStore,
                            parse.GetValue(model.ScanSource)!,
                            parse.GetValue(model.ScanOutput)!,
                            recurse: !parse.GetValue(model.ScanNoRecurse),
                            parse.GetValue(model.ScanFingerprint) ?? "standard",
                            parse.GetValue(model.ScanMaxFiles),
                            parse.GetValue(model.ScanMode) ?? "safe-copy",
                            parse.GetValue(model.ScanBackupRoot),
                            mlEngine,
                            modelIdentity,
                            cancellationToken).GetAwaiter().GetResult();
                    }
                    finally
                    {
                        mlRuntime?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                }

                var jpegtranPath = OrientExpress.Infrastructure.Imaging.JpegtranLocator.Locate(
                    hostEnvironment.EnvironmentVariables);
                if (jpegtranPath is null)
                {
                    context.WriteError(
                        CliExitCode.CapabilityUnavailable, "JPEGTRAN_NOT_FOUND",
                        "the lossless JPEG tool is not installed",
                        "install libjpeg-turbo (DEP-008); see 'upright doctor'");
                    return (int)CliExitCode.CapabilityUnavailable;
                }

                using var metadata = CreateMetadataService(hostEnvironment);
                if (!metadata.Identify().IsSuccess)
                {
                    context.WriteError(
                        CliExitCode.CapabilityUnavailable, "EXIFTOOL_NOT_FOUND",
                        "the metadata tool is not installed",
                        "install ExifTool (DEP-007); see 'upright doctor'");
                    return (int)CliExitCode.CapabilityUnavailable;
                }

                return ApplyCommand.RunAsync(
                    context,
                    jobStore,
                    identity,
                    new OrientExpress.Infrastructure.Imaging.JpegtranTransformer(jpegtranPath, metadata),
                    new OrientExpress.Infrastructure.Imaging.ProbeOutputVerifier(
                        new OrientExpress.Infrastructure.Imaging.ManagedFormatProber()),
                    new OrientExpress.Infrastructure.Windows.WindowsAtomicFileOperations(),
                    new OrientExpress.Infrastructure.Windows.WindowsVolumeCapabilityProbe(),
                    parse.GetValue(model.ApplyJob)!,
                    parse.GetValue(model.ApplyResume),
                    parse.GetValue(model.ApplyAcknowledgeInPlace),
                    cancellationToken).GetAwaiter().GetResult();
            }
            finally
            {
                jobStore.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
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
