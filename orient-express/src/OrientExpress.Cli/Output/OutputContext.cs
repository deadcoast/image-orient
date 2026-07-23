using OrientExpress.Contracts.Cli;

namespace OrientExpress.Cli.Output;

public enum OutputFormat
{
    Human,
    Json,
    Jsonl,
}

/// <summary>
/// Resolved global output contract for one invocation (DOC-CLI). Machine
/// stdout carries only contract documents; progress and diagnostics go to
/// stderr; nothing here ever emits ANSI styling (NFR-UX-002).
/// </summary>
public sealed record OutputContext(
    OutputFormat Format,
    bool NoColor,
    string Verbosity,
    string? CorrelationId,
    bool NonInteractive,
    TextWriter Out,
    TextWriter Error)
{
    public bool Machine => Format != OutputFormat.Human;

    public bool Quiet => Verbosity == "quiet";

    /// <summary>DOC-CLI: no progress when non-interactive; never on machine stdout.</summary>
    public void Progress(string message)
    {
        if (!NonInteractive)
        {
            Error.WriteLine("progress: " + message);
        }
    }

    /// <summary>
    /// Stable error line: symbol, reason code, safe message, next action
    /// (DOC-CLI human-output design). Always stderr.
    /// </summary>
    public void WriteError(CliExitCode exitCode, string reasonCode, string message, string nextAction)
        => Error.WriteLine(
            $"error [{CliExitCodeRegistry.SymbolOf(exitCode)}] [{reasonCode}]: {message}; next: {nextAction}");
}
