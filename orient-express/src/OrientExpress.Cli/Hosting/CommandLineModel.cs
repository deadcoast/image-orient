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

    public required Command Completion { get; init; }

    public required Argument<string> CompletionShell { get; init; }

    public required Command ConfigInit { get; init; }

    public required Option<string> ConfigInitPath { get; init; }

    public required Option<bool> ConfigInitForce { get; init; }

    public required Command ConfigShow { get; init; }

    public required Option<bool> ConfigShowRedacted { get; init; }

    public required Command ConfigValidate { get; init; }

    public required IReadOnlyDictionary<Command, GatedCommandSpec> Gated { get; init; }

    private static readonly GatedCommandSpec[] GatedSpecs =
    [
        new("inspect", "Explain identity, orientation evidence, and the proposed action for one file.", "WP-004;WP-005;WP-007"),
        new("scan", "Read-only planning; seals an immutable job plan.", "WP-004;WP-007;WP-008;WP-009"),
        new("apply", "Execute a sealed plan; never re-decides transforms.", "WP-009;WP-010;WP-011;WP-012"),
        new("run", "Scan then apply when policy permits (provisional).", "WP-009;WP-014"),
        new("verify", "Independently re-verify job outputs.", "WP-013"),
        new("undo", "Restore from verified backups for an in-place job.", "WP-012;WP-015"),
        new("review list", "List review-queue items with filters.", "WP-016"),
        new("review show", "Show one review item.", "WP-016"),
        new("review decide", "Record an append-only review override.", "WP-016"),
        new("review export", "Export review decisions.", "WP-016"),
        new("review import", "Import validated review decisions.", "WP-016"),
        new("jobs list", "List jobs.", "WP-017"),
        new("jobs show", "Show one job.", "WP-017"),
        new("jobs integrity", "Run job integrity checks.", "WP-017"),
        new("jobs prune", "Prune eligible jobs (dry-run by default).", "WP-017"),
        new("models list", "List installed models.", "WP-006"),
        new("models verify", "Verify model digests.", "WP-006"),
        new("models install", "Install a model artifact.", "WP-006"),
        new("models activate", "Activate an installed model.", "WP-006"),
        new("models remove", "Remove an installed model.", "WP-006"),
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
        var doctor = new Command(
            "doctor", "Environment and capability diagnostics; never writes or modifies images.");

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
        root.Subcommands.Add(completion);
        root.Subcommands.Add(configCommand);

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
            Completion = completion,
            CompletionShell = completionShell,
            ConfigInit = configInit,
            ConfigInitPath = configInitPath,
            ConfigInitForce = configInitForce,
            ConfigShow = configShow,
            ConfigShowRedacted = configShowRedacted,
            ConfigValidate = configValidate,
            Gated = gated,
        };
    }
}
