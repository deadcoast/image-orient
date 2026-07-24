using System.Text.Json.Serialization;

namespace OrientExpress.Contracts.Models;

/// <summary>
/// Model bundle manifest mirroring <c>schemas/model-manifest.schema.json</c>
/// (PRD-FR-020, ADR-0010). Parsing is permissive (the schema allows
/// additional properties in several sections); required fields and enums are
/// enforced by code-side validation before any bundle is accepted.
/// </summary>
public sealed record ModelManifest
{
    [JsonPropertyName("schemaVersion")]
    public string? SchemaVersion { get; init; }

    [JsonPropertyName("modelId")]
    public string? ModelId { get; init; }

    [JsonPropertyName("version")]
    public string? Version { get; init; }

    [JsonPropertyName("task")]
    public string? Task { get; init; }

    [JsonPropertyName("artifact")]
    public ModelArtifact? Artifact { get; init; }

    [JsonPropertyName("inputs")]
    public IReadOnlyList<TensorSpec>? Inputs { get; init; }

    [JsonPropertyName("outputs")]
    public IReadOnlyList<TensorSpec>? Outputs { get; init; }

    [JsonPropertyName("preprocessing")]
    public PreprocessingSpec? Preprocessing { get; init; }

    [JsonPropertyName("postprocessing")]
    public PostprocessingSpec? Postprocessing { get; init; }

    [JsonPropertyName("compatibility")]
    public CompatibilitySpec? Compatibility { get; init; }

    [JsonPropertyName("provenance")]
    public string? Provenance { get; init; }

    [JsonPropertyName("license")]
    public string? License { get; init; }
}

public sealed record ModelArtifact
{
    [JsonPropertyName("file")]
    public string? File { get; init; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; init; }

    [JsonPropertyName("bytes")]
    public long? Bytes { get; init; }

    [JsonPropertyName("format")]
    public string? Format { get; init; }
}

public sealed record TensorSpec
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("shape")]
    public IReadOnlyList<int>? Shape { get; init; }

    [JsonPropertyName("dataType")]
    public string? DataType { get; init; }
}

public sealed record PreprocessingSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("colorOrder")]
    public string? ColorOrder { get; init; }

    [JsonPropertyName("resize")]
    public string? Resize { get; init; }
}

public sealed record PostprocessingSpec
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}

public sealed record CompatibilitySpec
{
    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; init; }

    [JsonPropertyName("runtimeApi")]
    public string? RuntimeApi { get; init; }

    [JsonPropertyName("allowedProviders")]
    public IReadOnlyList<string>? AllowedProviders { get; init; }
}

/// <summary>A bundle as installed in the registry.</summary>
public sealed record InstalledModel(
    ModelManifest Manifest,
    string Directory,
    string ArtifactPath,
    bool IsActive);

/// <summary>The runtime identity exposed for provenance (DOC-COMPONENTS RuntimeDescriptor).</summary>
public sealed record RuntimeDescriptor(string Runtime, string Version, string Provider);

/// <summary>A verified, loadable reference to an installed model artifact.</summary>
public sealed record ModelHandle(string ModelId, string Version, string Sha256, string ArtifactPath);

public sealed record TensorInput(string Name, float[] Values, int[] Shape);

public sealed record TensorOutput(IReadOnlyDictionary<string, float[]> Values);

public sealed record InferenceOptions
{
    public static InferenceOptions Default { get; } = new();
}
