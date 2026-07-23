using System.Text.Json.Serialization;

namespace OrientExpress.Contracts.Config;

/// <summary>One validation finding; codes are stable uppercase identifiers (DOC-CONFIG).</summary>
public sealed record ConfigIssue(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

/// <summary>
/// The fully-resolved configuration with per-setting provenance (which
/// precedence layer supplied each value) and the canonical digest that plan
/// sealing records (DOC-CONFIG: apply uses the sealed configuration).
/// </summary>
public sealed record EffectiveConfig(
    [property: JsonPropertyName("config")] UprightConfig Config,
    [property: JsonPropertyName("provenance")] IReadOnlyDictionary<string, string> Provenance,
    [property: JsonPropertyName("digest")] string Digest);
