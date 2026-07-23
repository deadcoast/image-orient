using System.CommandLine;
using OrientExpress.Cli.Output;

namespace OrientExpress.Cli.Hosting;

/// <summary>Validates and resolves the DOC-CLI global output options.</summary>
public static class OutputContextResolver
{
    private static readonly string[] Verbosities = ["quiet", "normal", "detailed", "diagnostic"];

    public static bool TryResolve(
        ParseResult parse,
        CommandLineModel model,
        TextWriter stdout,
        TextWriter stderr,
        out OutputContext context,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(parse);
        ArgumentNullException.ThrowIfNull(model);
        context = null!;

        var json = parse.GetValue(model.JsonShortcut);
        var jsonl = parse.GetValue(model.JsonlShortcut);
        if (json && jsonl)
        {
            error = "--json and --jsonl are mutually exclusive";
            return false;
        }

        var formatValue = parse.GetValue(model.OutputFormat) ?? "human";
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
        if (!Verbosities.Contains(verbosity, StringComparer.Ordinal))
        {
            error = $"--verbosity value '{verbosity}' is not in the contract";
            return false;
        }

        var correlationId = parse.GetValue(model.CorrelationId);
        if (correlationId is { Length: > 128 })
        {
            error = "--correlation-id must be 128 characters or fewer";
            return false;
        }

        context = new OutputContext(
            format,
            parse.GetValue(model.NoColor),
            verbosity,
            correlationId,
            parse.GetValue(model.NonInteractive),
            stdout,
            stderr);
        error = null;
        return true;
    }
}
