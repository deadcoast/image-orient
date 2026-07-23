namespace OrientExpress.Spike009.Contract;

public enum OutputFormat
{
    Human,
    Json,
    Jsonl,
}

/// <summary>
/// Resolved global output contract for one invocation. Machine stdout carries
/// only the contract document(s); progress and diagnostics go to Error.
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

    /// <summary>DOC-CLI: no spinner/progress when non-interactive; never on machine stdout.</summary>
    public void Progress(string message)
    {
        if (!NonInteractive)
        {
            Error.WriteLine("progress: " + message);
        }
    }

    public void Diagnostic(string message) => Error.WriteLine(message);
}
