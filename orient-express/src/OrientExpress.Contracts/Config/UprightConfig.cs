using System.Text.Json.Serialization;

namespace OrientExpress.Contracts.Config;

/// <summary>
/// Configuration tree mirroring <c>schemas/config.schema.json</c> (DOC-CONFIG,
/// NFR-MAINT-002). Every leaf is nullable so a single record type expresses
/// both partial layers (file/env/CLI overlays) and the fully-resolved
/// effective configuration. Unknown members are rejected at parse time
/// (AC-037): strict is the only mode until a read-only consumer needs more.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UprightConfig
{
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; init; }

    [JsonPropertyName("paths")]
    public PathsConfig? Paths { get; init; }

    [JsonPropertyName("discovery")]
    public DiscoveryConfig? Discovery { get; init; }

    [JsonPropertyName("analysis")]
    public AnalysisConfig? Analysis { get; init; }

    [JsonPropertyName("output")]
    public OutputConfig? Output { get; init; }

    [JsonPropertyName("resources")]
    public ResourcesConfig? Resources { get; init; }

    [JsonPropertyName("privacy")]
    public PrivacyConfig? Privacy { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PathsConfig
{
    [JsonPropertyName("jobRoot")]
    public string? JobRoot { get; init; }

    [JsonPropertyName("modelRoot")]
    public string? ModelRoot { get; init; }

    [JsonPropertyName("cacheRoot")]
    public string? CacheRoot { get; init; }

    [JsonPropertyName("backupRoot")]
    public string? BackupRoot { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiscoveryConfig
{
    [JsonPropertyName("recurse")]
    public bool? Recurse { get; init; }

    [JsonPropertyName("followReparsePoints")]
    public bool? FollowReparsePoints { get; init; }

    [JsonPropertyName("include")]
    public IReadOnlyList<string>? Include { get; init; }

    [JsonPropertyName("exclude")]
    public IReadOnlyList<string>? Exclude { get; init; }

    [JsonPropertyName("maxFiles")]
    public long? MaxFiles { get; init; }

    [JsonPropertyName("maxBytes")]
    public long? MaxBytes { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnalysisConfig
{
    [JsonPropertyName("profile")]
    public string? Profile { get; init; }

    [JsonPropertyName("maxThumbnailLongEdge")]
    public int? MaxThumbnailLongEdge { get; init; }

    [JsonPropertyName("faceModel")]
    public string? FaceModel { get; init; }

    [JsonPropertyName("sceneFallback")]
    public bool? SceneFallback { get; init; }

    [JsonPropertyName("sequenceConsensus")]
    public bool? SequenceConsensus { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OutputConfig
{
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("collision")]
    public string? Collision { get; init; }

    [JsonPropertyName("jpegEdge")]
    public string? JpegEdge { get; init; }

    [JsonPropertyName("metadataProfile")]
    public string? MetadataProfile { get; init; }

    [JsonPropertyName("preserveFilesystemTimes")]
    public bool? PreserveFilesystemTimes { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResourcesConfig
{
    [JsonPropertyName("memoryMiB")]
    public int? MemoryMiB { get; init; }

    [JsonPropertyName("metadataWorkers")]
    public int? MetadataWorkers { get; init; }

    [JsonPropertyName("decodeWorkers")]
    public int? DecodeWorkers { get; init; }

    [JsonPropertyName("inferenceWorkers")]
    public int? InferenceWorkers { get; init; }

    [JsonPropertyName("transformWorkers")]
    public int? TransformWorkers { get; init; }

    [JsonPropertyName("verificationWorkers")]
    public int? VerificationWorkers { get; init; }

    [JsonPropertyName("queueCapacity")]
    public int? QueueCapacity { get; init; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrivacyConfig
{
    [JsonPropertyName("telemetry")]
    public bool? Telemetry { get; init; }

    [JsonPropertyName("reviewThumbnails")]
    public bool? ReviewThumbnails { get; init; }

    [JsonPropertyName("redactPathsInDiagnostics")]
    public bool? RedactPathsInDiagnostics { get; init; }

    [JsonPropertyName("diagnosticRetentionDays")]
    public int? DiagnosticRetentionDays { get; init; }
}
