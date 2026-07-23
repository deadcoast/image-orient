using System.Text.Json.Serialization;

namespace OrientExpress.Contracts.Reporting;

/// <summary>
/// Conforms to <c>schemas/capability-report.schema.json</c> (AC-003).
/// Capability truth-telling is the contract: a component or format is
/// reported available only when its gating evidence exists.
/// </summary>
public sealed record CapabilityReport
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; init; } = "1.0";

    [JsonPropertyName("generatedUtc")]
    public required string GeneratedUtc { get; init; }

    [JsonPropertyName("tool")]
    public required IReadOnlyDictionary<string, string> Tool { get; init; }

    [JsonPropertyName("platform")]
    public required IReadOnlyDictionary<string, string> Platform { get; init; }

    [JsonPropertyName("components")]
    public required IReadOnlyList<ComponentCapability> Components { get; init; }

    [JsonPropertyName("formats")]
    public required IReadOnlyList<FormatCapability> Formats { get; init; }
}

public sealed record ComponentCapability
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("available")]
    public required bool Available { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }
}

public sealed record FormatCapability
{
    [JsonPropertyName("format")]
    public required string Format { get; init; }

    [JsonPropertyName("read")]
    public required bool Read { get; init; }

    [JsonPropertyName("metadataRead")]
    public required bool MetadataRead { get; init; }

    [JsonPropertyName("transform")]
    public required bool Transform { get; init; }

    [JsonPropertyName("metadataWrite")]
    public required bool MetadataWrite { get; init; }

    [JsonPropertyName("verify")]
    public required bool Verify { get; init; }

    [JsonPropertyName("reasonCodes")]
    public IReadOnlyList<string>? ReasonCodes { get; init; }
}
