using System.Text.Json.Serialization;

namespace OrientExpress.Contracts.Cli;

/// <summary>
/// Machine event conforming to <c>schemas/event.schema.json</c>: camelCase
/// names, the five required fields always present, optional fields omitted
/// when null, no properties outside the schema (ADR-0010).
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

    [JsonPropertyName("fileId")]
    public string? FileId { get; init; }

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("stage")]
    public string? Stage { get; init; }

    [JsonPropertyName("action")]
    public string? Action { get; init; }

    [JsonPropertyName("result")]
    public required string Result { get; init; }

    [JsonPropertyName("reasonCodes")]
    public IReadOnlyList<string>? ReasonCodes { get; init; }

    [JsonPropertyName("pathToken")]
    public string? PathToken { get; init; }
}
