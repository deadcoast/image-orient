using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrientExpress.Spike009.Contract;

/// <summary>
/// Machine event matching schemas/event.schema.json: camelCase names, the five
/// required fields always present, optional fields omitted when null, and no
/// additional properties outside the schema.
/// </summary>
public sealed record CliEvent
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("eventType")]
    public required string EventType { get; init; }

    [JsonPropertyName("eventId")]
    public required string EventId { get; init; }

    [JsonPropertyName("timestampUtc")]
    public required string TimestampUtc { get; init; }

    [JsonPropertyName("jobId")]
    public string? JobId { get; init; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("result")]
    public required string Result { get; init; }

    [JsonPropertyName("reasonCodes")]
    public IReadOnlyList<string>? ReasonCodes { get; init; }

    [JsonPropertyName("pathToken")]
    public string? PathToken { get; init; }

    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, int>? Data { get; init; }
}

public sealed class EventFactory(string command, string? jobId, string? correlationId)
{
    public static readonly JsonSerializerOptions LineOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public CliEvent Create(
        string eventType,
        string result,
        string? action = null,
        string? pathToken = null,
        IReadOnlyList<string>? reasonCodes = null,
        IReadOnlyDictionary<string, int>? data = null)
        => new()
        {
            EventType = eventType,
            EventId = Guid.NewGuid().ToString("N"),
            TimestampUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            JobId = jobId,
            CorrelationId = correlationId,
            Command = command,
            Action = action,
            Result = result,
            ReasonCodes = reasonCodes,
            PathToken = pathToken,
            Data = data,
        };

    public static string Serialize(CliEvent cliEvent)
        => JsonSerializer.Serialize(cliEvent, LineOptions);
}
