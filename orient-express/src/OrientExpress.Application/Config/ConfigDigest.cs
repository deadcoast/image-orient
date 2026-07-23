using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrientExpress.Contracts.Config;

namespace OrientExpress.Application.Config;

/// <summary>
/// Canonical effective-config digest (DOC-CONFIG): plan sealing records it,
/// and apply refuses a job whose sealed digest no longer matches. Property
/// order is fixed by the record declarations, so serialization is canonical.
/// </summary>
public static class ConfigDigest
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Compute(UprightConfig config)
        => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config, CanonicalOptions))));
}
