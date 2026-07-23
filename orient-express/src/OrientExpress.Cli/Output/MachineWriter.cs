using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrientExpress.Contracts.Cli;

namespace OrientExpress.Cli.Output;

/// <summary>Single-document machine result ("command-result type" per DOC-CLI machine rules).</summary>
public sealed record CommandResultDocument
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("resultType")]
    public string ResultType { get; init; } = "command.result";

    [JsonPropertyName("command")]
    public required string Command { get; init; }

    [JsonPropertyName("contractVersion")]
    public required string ContractVersion { get; init; }

    [JsonPropertyName("toolVersion")]
    public string? ToolVersion { get; init; }

    [JsonPropertyName("exitCode")]
    public required int ExitCode { get; init; }

    [JsonPropertyName("exitSymbol")]
    public required string ExitSymbol { get; init; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("reasonCodes")]
    public IReadOnlyList<string>? ReasonCodes { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("nextAction")]
    public string? NextAction { get; init; }
}

public static class MachineWriter
{
    /// <summary>Indented for readability; still exactly one document (AC-033).</summary>
    public static readonly JsonSerializerOptions DocumentOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    /// <summary>JSONL events must serialize to exactly one line.</summary>
    public static readonly JsonSerializerOptions LineOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static void WriteDocument<TDocument>(OutputContext context, TDocument document)
        => context.Out.WriteLine(JsonSerializer.Serialize(document, DocumentOptions));

    public static void WriteEventLine(OutputContext context, CliEvent cliEvent)
        => context.Out.WriteLine(JsonSerializer.Serialize(cliEvent, LineOptions));

    public static CliEvent NewEvent(
        OutputContext context,
        string command,
        string eventType,
        string result,
        IReadOnlyList<string>? reasonCodes = null,
        string? pathToken = null)
        => new()
        {
            EventType = eventType,
            EventId = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            Command = command,
            CorrelationId = context.CorrelationId,
            Result = result,
            ReasonCodes = reasonCodes,
            PathToken = pathToken,
        };
}
