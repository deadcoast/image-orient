using System.CommandLine;

namespace OrientExpress.Cli.Hosting;

/// <summary>A DOC-CLI command whose subsystem evidence does not exist yet.</summary>
public sealed record GatedCommandSpec(string Path, string Description, string RequiredWork);

/// <summary>
/// The DOC-CLI command tree. Implemented commands dispatch to real handlers;
/// every other documented command is registered but *gated*: it refuses with
/// CAPABILITY_UNAVAILABLE instead of pretending (DOC-DEVPLAN: incomplete
/// paths cannot masquerade as supported). One model feeds parsing, dispatch,
/// and completion generation.
/// </summary>
public sealed class CommandLineModel
{
    public required RootCommand Root { get; init; }

    public required Option<string> OutputFormat { get; init; }

    public required Option<bool> JsonShortcut { get; init; }

    public required Option<bool> JsonlShortcut { get; init; }

    public required Option<bool> NoColor { get; init; }

    public required Option<string> Verbosity { get; init; }

    public required Option<string> CorrelationId { get; init; }

    public required Option<bool> NonInteractive { get; init; }

    public required Option<string> ConfigPath { get; init; }

    public required Option<string> Profile { get; init; }

    public required Command Version { get; init; }

    public required Command Doctor { get; init; }

    public required Option<string> DoctorProbeTarget { get; init; }

    public required Command Inspect { get; init; }

    public required Argument<string> InspectFile { get; init; }

    public required Command Completion { get; init; }

    public required Argument<string> CompletionShell { get; init; }

    public required Command ConfigInit { get; init; }

    public required Option<string> ConfigInitPath { get; init; }

    public required Option<bool> ConfigInitForce { get; init; }

    public required Command ConfigShow { get; init; }

    public required Option<bool> ConfigShowRedacted { get; init; }

    public required Command ConfigValidate { get; init; }

    public required Command ModelsList { get; init; }

    public required Command ModelsInstall { get; init; }

    public required Argument<string> ModelsInstallArtifact { get; init; }

    public required Option<string> ModelsInstallManifest { get; init; }

    public required Command ModelsVerify { get; init; }

    public required Argument<string> ModelsVerifyId { get; init; }

    public required Argument<string> ModelsVerifyVersion { get; init; }

    public required Command ModelsActivate { get; init; }

    public required Argument<string> ModelsActivateId { get; init; }

    public required Argument<string> ModelsActivateVersion { get; init; }

    public required Command ModelsRollback { get; init; }

    public required Command ModelsRemove { get; init; }

    public required Argument<string> ModelsRemoveId { get; init; }

    public required Argument<string> ModelsRemoveVersion { get; init; }

    public required IReadOnlyDictionary<Command, GatedCommandSpec> Gated { get; init; }

    public required Command Scan { get; init; }

    public required Argument<string> ScanSource { get; init; }

    public required Option<string> ScanOutput { get; init; }

    public required Option<bool> ScanNoRecurse { get; init; }

    public required Option<string> ScanFingerprint { get; init; }

    public required Option<int?> ScanMaxFiles { get; init; }

    public required Option<string> ScanMode { get; init; }

    public required Option<string> ScanBackupRoot { get; init; }

    public required Command Apply { get; init; }

    public required Argument<string> ApplyJob { get; init; }

    public required Option<bool> ApplyResume { get; init; }

    public required Option<bool> ApplyAcknowledgeInPlace { get; init; }

    public required Command Undo { get; init; }

    public required Argument<string> UndoJob { get; init; }

    public required Option<bool> UndoYes { get; init; }

    public required Command Verify { get; init; }

    public required Argument<string> VerifyJob { get; init; }

    public required Command JobsList { get; init; }

    public required Command JobsShow { get; init; }

    public required Argument<string> JobsShowJob { get; init; }

    public required Command JobsIntegrity { get; init; }

    public required Argument<string> JobsIntegrityJob { get; init; }

    public required Command JobsPrune { get; init; }

    public required Argument<string> JobsPruneJob { get; init; }

    public required Option<bool> JobsPruneYes { get; init; }

    private static readonly GatedCommandSpec[] GatedSpecs =
    [
        new("run", "Scan then apply when policy permits (provisional).", "WP-009;WP-014"),
        new("review list", "List review-queue items with filters.", "WP-016"),
        new("review show", "Show one review item.", "WP-016"),
        new("review decide", "Record an append-only review override.", "WP-016"),
        new("review export", "Export review decisions.", "WP-016"),
        new("review import", "Import validated review decisions.", "WP-016"),
        new("benchmark", "Measure local throughput and latency.", "WP-021"),
    ];

    public static CommandLineModel Build()
    {
        var outputFormat = new Option<string>("--output-format")
        {
            Description = "Output contract: human, json, or jsonl.",
            Recursive = true,
        };
        var jsonShortcut = new Option<bool>("--json")
        {
            Description = "Alias for --output-format json.",
            Recursive = true,
        };
        var jsonlShortcut = new Option<bool>("--jsonl")
        {
            Description = "Alias for --output-format jsonl.",
            Recursive = true,
        };
        var noColor = new Option<bool>("--no-color")
        {
            Description = "Disable styling (the host currently never emits ANSI).",
            Recursive = true,
        };
        var verbosity = new Option<string>("--verbosity")
        {
            Description = "quiet, normal, detailed, or diagnostic; never changes machine schema.",
            Recursive = true,
        };
        var correlationId = new Option<string>("--correlation-id")
        {
            Description = "Caller-provided trace value echoed into machine output.",
            Recursive = true,
        };
        var nonInteractive = new Option<bool>("--non-interactive")
        {
            Description = "Refuse prompts and progress; required for deterministic automation.",
            Recursive = true,
        };
        var configPath = new Option<string>("--config")
        {
            Description = "Explicit configuration file (layer 3 of the precedence chain).",
            Recursive = true,
        };
        var profile = new Option<string>("--profile")
        {
            Description = "Analysis profile override (CLI layer): conservative, balanced, or expert.",
            Recursive = true,
        };

        var version = new Command("version", "Report tool and contract versions.");
        var doctorProbeTarget = new Option<string>("--probe-target")
        {
            Description = "Directory for filesystem operation probes (identity, atomic replace); files are cleaned up.",
        };
        var doctor = new Command(
            "doctor", "Environment and capability diagnostics; never reads or modifies images.");
        doctor.Options.Add(doctorProbeTarget);

        var inspectFile = new Argument<string>("file") { Description = "Image file to inspect (read-only)." };
        var inspect = new Command(
            "inspect", "Read-only diagnostic view: identity, format, and EXIF orientation. Decisions are gated until WP-007.");
        inspect.Arguments.Add(inspectFile);

        var completionShell = new Argument<string>("shell") { Description = "Target shell (powershell)." };
        var completion = new Command("completion", "Emit a shell completion script from the command model.");
        completion.Arguments.Add(completionShell);

        var configInitPath = new Option<string>("--path")
        {
            Description = "Destination file (defaults to the user configuration path).",
        };
        var configInitForce = new Option<bool>("--force") { Description = "Overwrite an existing file." };
        var configInit = new Command("init", "Write a starter configuration with the built-in defaults.");
        configInit.Options.Add(configInitPath);
        configInit.Options.Add(configInitForce);

        var configShowRedacted = new Option<bool>("--redacted")
        {
            Description = "Replace path values with digests (canonical support view).",
        };
        var configShow = new Command("show", "Show the effective configuration with per-setting provenance.");
        configShow.Options.Add(configShowRedacted);

        var configValidate = new Command("validate", "Validate the effective configuration; exits 21 on issues.");

        var modelsList = new Command("list", "List installed model bundles; the active one is marked.");
        var modelsInstallArtifact = new Argument<string>("artifact") { Description = "Path to the local .onnx artifact." };
        var modelsInstallManifest = new Option<string>("--manifest")
        {
            Description = "Path to the bundle manifest (model-manifest.schema.json).",
            Required = true,
        };
        var modelsInstall = new Command(
            "install", "Install a digest-pinned local model bundle; failures quarantine, never activate.");
        modelsInstall.Arguments.Add(modelsInstallArtifact);
        modelsInstall.Options.Add(modelsInstallManifest);

        var modelsVerifyId = new Argument<string>("model-id") { Arity = ArgumentArity.ZeroOrOne };
        var modelsVerifyVersion = new Argument<string>("version") { Arity = ArgumentArity.ZeroOrOne };
        var modelsVerify = new Command("verify", "Re-verify installed bundle digests (all, or one id + version).");
        modelsVerify.Arguments.Add(modelsVerifyId);
        modelsVerify.Arguments.Add(modelsVerifyVersion);

        var modelsActivateId = new Argument<string>("model-id");
        var modelsActivateVersion = new Argument<string>("version");
        var modelsActivate = new Command("activate", "Atomically activate a verified bundle (prior model is recorded).");
        modelsActivate.Arguments.Add(modelsActivateId);
        modelsActivate.Arguments.Add(modelsActivateVersion);

        var modelsRollback = new Command("rollback", "Restore the previously active verified bundle.");

        var modelsRemoveId = new Argument<string>("model-id");
        var modelsRemoveVersion = new Argument<string>("version");
        var modelsRemove = new Command("remove", "Remove an installed bundle (the active model is refused).");
        modelsRemove.Arguments.Add(modelsRemoveId);
        modelsRemove.Arguments.Add(modelsRemoveVersion);

        var modelsCommand = new Command("models", "Model bundle lifecycle: digest-pinned, quarantining, rollback-safe.");
        modelsCommand.Subcommands.Add(modelsList);
        modelsCommand.Subcommands.Add(modelsInstall);
        modelsCommand.Subcommands.Add(modelsVerify);
        modelsCommand.Subcommands.Add(modelsActivate);
        modelsCommand.Subcommands.Add(modelsRollback);
        modelsCommand.Subcommands.Add(modelsRemove);

        var scanSource = new Argument<string>("source") { Description = "Folder of photos to scan (read-only)." };
        var scanOutput = new Option<string>("--output")
        {
            Description = "Destination root for the safe-copy outputs (required).",
            Required = true,
        };
        var scanNoRecurse = new Option<bool>("--no-recurse") { Description = "Scan only the top directory." };
        var scanFingerprint = new Option<string>("--fingerprint")
        {
            Description = "standard (size+time+prefix digest) or strong (full SHA-256).",
        };
        var scanMaxFiles = new Option<int?>("--max-files") { Description = "Guardrail on files considered." };
        var scanMode = new Option<string>("--mode")
        {
            Description = "safe-copy (default; originals untouched) or in-place (guarded rewrite with verified backups).",
        };
        var scanBackupRoot = new Option<string>("--backup-root")
        {
            Description = "Backup destination; required for --mode in-place.",
        };
        var scan = new Command(
            "scan",
            "Read-only planning; seals an immutable job plan. Deterministic mode: only files whose "
            + "EXIF orientation tag proves a rotation are planned; ML decisions stay gated (WP-007/008).");
        scan.Arguments.Add(scanSource);
        scan.Options.Add(scanOutput);
        scan.Options.Add(scanNoRecurse);
        scan.Options.Add(scanFingerprint);
        scan.Options.Add(scanMaxFiles);
        scan.Options.Add(scanMode);
        scan.Options.Add(scanBackupRoot);

        var applyJob = new Argument<string>("job") { Description = "Job id from a sealed scan." };
        var applyResume = new Option<bool>("--resume")
        {
            Description = "Idempotently skip rows whose commit event already exists.",
        };
        var apply = new Command(
            "apply",
            "Execute a sealed plan (safe-copy): stale inputs refused, outputs verified before the atomic "
            + "commit, sources never written.");
        var applyAcknowledgeInPlace = new Option<bool>("--acknowledge-in-place")
        {
            Description = "Required for in-place jobs: originals are rewritten (verified backups kept).",
        };
        apply.Arguments.Add(applyJob);
        apply.Options.Add(applyResume);
        apply.Options.Add(applyAcknowledgeInPlace);

        var undoJob = new Argument<string>("job") { Description = "In-place job whose backups to restore." };
        var undoYes = new Option<bool>("--yes") { Description = "Confirm restoring current files from backups." };
        var undo = new Command(
            "undo",
            "Restore verified backups for an in-place job; files that changed since apply are conflicts, never forced.");
        undo.Arguments.Add(undoJob);
        undo.Options.Add(undoYes);

        var verifyJob = new Argument<string>("job") { Description = "Job whose committed outputs to re-verify." };
        var verify = new Command(
            "verify", "Independently re-verify committed outputs through the independent read path (read-only).");
        verify.Arguments.Add(verifyJob);

        var jobsList = new Command("list", "List jobs under the configured job root.");
        var jobsShowJob = new Argument<string>("job");
        var jobsShow = new Command("show", "Show one job: manifest, seal, plan counts, committed rows.");
        jobsShow.Arguments.Add(jobsShowJob);
        var jobsIntegrityJob = new Argument<string>("job") { Arity = ArgumentArity.ZeroOrOne };
        var jobsIntegrity = new Command("integrity", "Integrity-check one job, or every job when none is named.");
        jobsIntegrity.Arguments.Add(jobsIntegrityJob);
        var jobsPruneJob = new Argument<string>("job");
        var jobsPruneYes = new Option<bool>("--yes") { Description = "Actually remove; dry-run is the default." };
        var jobsPrune = new Command("prune", "Remove a job directory (dry-run by default; recovery-pending jobs refuse).");
        jobsPrune.Arguments.Add(jobsPruneJob);
        jobsPrune.Options.Add(jobsPruneYes);
        var jobsCommand = new Command("jobs", "Job inspection and maintenance; never applies image changes.");
        jobsCommand.Subcommands.Add(jobsList);
        jobsCommand.Subcommands.Add(jobsShow);
        jobsCommand.Subcommands.Add(jobsIntegrity);
        jobsCommand.Subcommands.Add(jobsPrune);

        var configCommand = new Command("config", "Configuration operations (precedence: CLI > env > --config > user file > defaults).");
        configCommand.Subcommands.Add(configInit);
        configCommand.Subcommands.Add(configShow);
        configCommand.Subcommands.Add(configValidate);

        var root = new RootCommand(
            "upright — offline photo orientation for Windows. Diagnostic-mode alpha: " +
            "commands whose safety evidence is pending refuse to run (see STATUS.md).");
        root.Options.Add(outputFormat);
        root.Options.Add(jsonShortcut);
        root.Options.Add(jsonlShortcut);
        root.Options.Add(noColor);
        root.Options.Add(verbosity);
        root.Options.Add(correlationId);
        root.Options.Add(nonInteractive);
        root.Options.Add(configPath);
        root.Options.Add(profile);
        root.Subcommands.Add(version);
        root.Subcommands.Add(doctor);
        root.Subcommands.Add(inspect);
        root.Subcommands.Add(scan);
        root.Subcommands.Add(apply);
        root.Subcommands.Add(verify);
        root.Subcommands.Add(undo);
        root.Subcommands.Add(jobsCommand);
        root.Subcommands.Add(completion);
        root.Subcommands.Add(configCommand);
        root.Subcommands.Add(modelsCommand);

        var gated = new Dictionary<Command, GatedCommandSpec>();
        var parents = new Dictionary<string, Command>(StringComparer.Ordinal);
        foreach (var spec in GatedSpecs)
        {
            var segments = spec.Path.Split(' ');
            var leaf = new Command(segments[^1], spec.Description + " [gated: requires " + spec.RequiredWork + "]");
            // Gated leaves swallow any arguments so callers get the honest
            // capability refusal rather than a grammar error.
            leaf.Arguments.Add(new Argument<string[]>("args")
            {
                Arity = ArgumentArity.ZeroOrMore,
                Hidden = true,
            });
            if (segments.Length == 1)
            {
                root.Subcommands.Add(leaf);
            }
            else
            {
                if (!parents.TryGetValue(segments[0], out var parent))
                {
                    parent = new Command(segments[0], "Namespace for '" + segments[0] + "' operations.");
                    parents[segments[0]] = parent;
                    root.Subcommands.Add(parent);
                }

                parent.Subcommands.Add(leaf);
            }

            gated[leaf] = spec;
        }

        return new CommandLineModel
        {
            Root = root,
            OutputFormat = outputFormat,
            JsonShortcut = jsonShortcut,
            JsonlShortcut = jsonlShortcut,
            NoColor = noColor,
            Verbosity = verbosity,
            CorrelationId = correlationId,
            NonInteractive = nonInteractive,
            ConfigPath = configPath,
            Profile = profile,
            Version = version,
            Doctor = doctor,
            DoctorProbeTarget = doctorProbeTarget,
            Inspect = inspect,
            InspectFile = inspectFile,
            Completion = completion,
            CompletionShell = completionShell,
            ConfigInit = configInit,
            ConfigInitPath = configInitPath,
            ConfigInitForce = configInitForce,
            ConfigShow = configShow,
            ConfigShowRedacted = configShowRedacted,
            ConfigValidate = configValidate,
            ModelsList = modelsList,
            ModelsInstall = modelsInstall,
            ModelsInstallArtifact = modelsInstallArtifact,
            ModelsInstallManifest = modelsInstallManifest,
            ModelsVerify = modelsVerify,
            ModelsVerifyId = modelsVerifyId,
            ModelsVerifyVersion = modelsVerifyVersion,
            ModelsActivate = modelsActivate,
            ModelsActivateId = modelsActivateId,
            ModelsActivateVersion = modelsActivateVersion,
            ModelsRollback = modelsRollback,
            ModelsRemove = modelsRemove,
            ModelsRemoveId = modelsRemoveId,
            ModelsRemoveVersion = modelsRemoveVersion,
            Scan = scan,
            ScanSource = scanSource,
            ScanOutput = scanOutput,
            ScanNoRecurse = scanNoRecurse,
            ScanFingerprint = scanFingerprint,
            ScanMaxFiles = scanMaxFiles,
            Apply = apply,
            ApplyJob = applyJob,
            ApplyResume = applyResume,
            ApplyAcknowledgeInPlace = applyAcknowledgeInPlace,
            ScanMode = scanMode,
            ScanBackupRoot = scanBackupRoot,
            Undo = undo,
            UndoJob = undoJob,
            UndoYes = undoYes,
            Verify = verify,
            VerifyJob = verifyJob,
            JobsList = jobsList,
            JobsShow = jobsShow,
            JobsShowJob = jobsShowJob,
            JobsIntegrity = jobsIntegrity,
            JobsIntegrityJob = jobsIntegrityJob,
            JobsPrune = jobsPrune,
            JobsPruneJob = jobsPruneJob,
            JobsPruneYes = jobsPruneYes,
            Gated = gated,
        };
    }
}
