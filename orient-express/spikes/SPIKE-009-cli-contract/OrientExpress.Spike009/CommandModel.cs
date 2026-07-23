using System.CommandLine;

namespace OrientExpress.Spike009;

/// <summary>
/// The representative DOC-CLI command slice under qualification. Held as
/// object references so dispatch and completion generation share one model.
/// </summary>
public sealed class CommandModel
{
    public required RootCommand Root { get; init; }

    public required Option<string> OutputFormat { get; init; }

    public required Option<bool> JsonShortcut { get; init; }

    public required Option<bool> JsonlShortcut { get; init; }

    public required Option<bool> NoColor { get; init; }

    public required Option<string> Verbosity { get; init; }

    public required Option<string> CorrelationId { get; init; }

    public required Option<bool> NonInteractive { get; init; }

    public required Command Version { get; init; }

    public required Command Doctor { get; init; }

    public required Command Inspect { get; init; }

    public required Argument<string> InspectFile { get; init; }

    public required Command Scan { get; init; }

    public required Argument<string[]> ScanPaths { get; init; }

    public required Option<string?> JobDirectory { get; init; }

    public required Option<string> Mode { get; init; }

    public required Option<int> CancelAfter { get; init; }

    public required Command Apply { get; init; }

    public required Argument<string> ApplyJob { get; init; }

    public required Option<bool> AcknowledgeInPlace { get; init; }

    public required Command Completion { get; init; }

    public required Argument<string> CompletionShell { get; init; }

    public static CommandModel Build()
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
            Description = "Disable ANSI styling.",
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

        var version = new Command("version", "Report tool and contract versions.");
        var doctor = new Command("doctor", "Environment and capability checks without image mutation.");

        var inspectFile = new Argument<string>("file") { Description = "Path token to inspect." };
        var inspect = new Command("inspect", "Explain the decision for one file.");
        inspect.Arguments.Add(inspectFile);

        var scanPaths = new Argument<string[]>("paths")
        {
            Description = "Path tokens to plan.",
            Arity = ArgumentArity.OneOrMore,
        };
        var jobDirectory = new Option<string?>("--job") { Description = "Job directory receiving the sealed plan." };
        var mode = new Option<string>("--mode") { Description = "safe-copy (default) or in-place." };
        var cancelAfter = new Option<int>("--simulate-cancel-after")
        {
            Description = "Prototype-only: cancel after N items to exercise CANCELLED.",
        };
        var scan = new Command("scan", "Read-only planning; seals an immutable job plan.");
        scan.Arguments.Add(scanPaths);
        scan.Options.Add(jobDirectory);
        scan.Options.Add(mode);
        scan.Options.Add(cancelAfter);

        var applyJob = new Argument<string>("job") { Description = "Job directory containing a sealed plan." };
        var acknowledgeInPlace = new Option<bool>("--acknowledge-in-place")
        {
            Description = "Required to apply an in-place plan.",
        };
        var apply = new Command("apply", "Execute a sealed plan; never re-decides transforms.");
        apply.Arguments.Add(applyJob);
        apply.Options.Add(acknowledgeInPlace);

        var completionShell = new Argument<string>("shell") { Description = "Target shell (powershell)." };
        var completion = new Command("completion", "Emit a shell completion script from the command model.");
        completion.Arguments.Add(completionShell);

        var root = new RootCommand("orient-express CLI contract prototype (SPIKE-009; not the product CLI).");
        root.Options.Add(outputFormat);
        root.Options.Add(jsonShortcut);
        root.Options.Add(jsonlShortcut);
        root.Options.Add(noColor);
        root.Options.Add(verbosity);
        root.Options.Add(correlationId);
        root.Options.Add(nonInteractive);
        root.Subcommands.Add(version);
        root.Subcommands.Add(doctor);
        root.Subcommands.Add(inspect);
        root.Subcommands.Add(scan);
        root.Subcommands.Add(apply);
        root.Subcommands.Add(completion);

        return new CommandModel
        {
            Root = root,
            OutputFormat = outputFormat,
            JsonShortcut = jsonShortcut,
            JsonlShortcut = jsonlShortcut,
            NoColor = noColor,
            Verbosity = verbosity,
            CorrelationId = correlationId,
            NonInteractive = nonInteractive,
            Version = version,
            Doctor = doctor,
            Inspect = inspect,
            InspectFile = inspectFile,
            Scan = scan,
            ScanPaths = scanPaths,
            JobDirectory = jobDirectory,
            Mode = mode,
            CancelAfter = cancelAfter,
            Apply = apply,
            ApplyJob = applyJob,
            AcknowledgeInPlace = acknowledgeInPlace,
            Completion = completion,
            CompletionShell = completionShell,
        };
    }
}
