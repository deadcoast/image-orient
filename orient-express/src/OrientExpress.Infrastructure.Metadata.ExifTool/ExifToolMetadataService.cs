using System.Security.Cryptography;
using System.Text.Json;
using OrientExpress.Application.Metadata;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Metadata;

namespace OrientExpress.Infrastructure.Metadata.ExifTool;

/// <summary>
/// COMP-METADATA adapter (ADR-0006, DEP-007). Every failure is a typed
/// result; the adapter degrades cleanly when the tool is absent so doctor
/// and inspect can truth-tell instead of crashing.
/// </summary>
public sealed class ExifToolMetadataService : IMetadataService
{
    /// <summary>Volatile groups excluded from field-aware comparison (filesystem state, not metadata).</summary>
    private static readonly string[] VolatilePrefixes = ["System:", "File:", "ExifTool:"];

    private readonly ExifToolStayOpenClient? _client;
    private (string Version, string Sha256)? _identity;

    public ExifToolMetadataService(string? exifToolPath)
        => _client = exifToolPath is null ? null : new ExifToolStayOpenClient(exifToolPath);

    public Result<(string Version, string Sha256)> Identify()
    {
        if (_client is null)
        {
            return NotAvailable<(string, string)>();
        }

        try
        {
            if (_identity is null)
            {
                using var stream = File.OpenRead(_client.ExifToolPath);
                _identity = (_client.Version(), Convert.ToHexStringLower(SHA256.HashData(stream)));
            }

            return Result.Ok(_identity.Value);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            return Result.Fail<(string, string)>(
                "EXIFTOOL_FAILED", ErrorCategory.Unexpected, true, exception.Message);
        }
    }

    public ValueTask<Result<MetadataSnapshot>> ReadAsync(NormalizedPath path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        cancellationToken.ThrowIfCancellationRequested();
        if (_client is null)
        {
            return ValueTask.FromResult(NotAvailable<MetadataSnapshot>());
        }

        try
        {
            var json = _client.ReadAllTagsJson(path.Display);
            if (string.IsNullOrWhiteSpace(json))
            {
                return ValueTask.FromResult(Result.Fail<MetadataSnapshot>(
                    "METADATA_UNREADABLE", ErrorCategory.InvalidInput, false,
                    $"exiftool produced no metadata for '{path.Display}'"));
            }

            return ValueTask.FromResult(Result.Ok(ParseSnapshot(path.Display, json)));
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException)
        {
            return ValueTask.FromResult(Result.Fail<MetadataSnapshot>(
                "EXIFTOOL_FAILED", ErrorCategory.Unexpected, true, exception.Message));
        }
    }

    public ValueTask<Result<bool>> WriteOrientationAsync(
        NormalizedPath path, int orientationValue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        cancellationToken.ThrowIfCancellationRequested();
        if (orientationValue is < 1 or > 8)
        {
            return ValueTask.FromResult(Result.Fail<bool>(
                "ORIENTATION_OUT_OF_RANGE", ErrorCategory.InvalidInput, false,
                "EXIF orientation must be 1..8"));
        }

        if (_client is null)
        {
            return ValueTask.FromResult(NotAvailable<bool>());
        }

        try
        {
            var response = _client.WriteOrientation(path.Display, orientationValue);
            return ValueTask.FromResult(
                response.Output.Contains("1 image files updated", StringComparison.OrdinalIgnoreCase)
                    ? Result.Ok(true)
                    : Result.Fail<bool>(
                        "METADATA_WRITE_REFUSED", ErrorCategory.InvalidInput, false,
                        response.StderrOutput.Trim().Length > 0 ? response.StderrOutput.Trim() : response.Output.Trim()));
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            return ValueTask.FromResult(Result.Fail<bool>(
                "EXIFTOOL_FAILED", ErrorCategory.Unexpected, true, exception.Message));
        }
    }

    public async ValueTask<Result<MetadataDiff>> CompareAsync(
        MetadataSnapshot before, NormalizedPath path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(before);
        var current = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (!current.IsSuccess)
        {
            return Result.Fail<MetadataDiff>(current.Error!);
        }

        var after = current.Require();
        var added = after.Tags.Keys
            .Where(key => !before.Tags.ContainsKey(key) && !IsVolatile(key)).Order(StringComparer.Ordinal).ToList();
        var removed = before.Tags.Keys
            .Where(key => !after.Tags.ContainsKey(key) && !IsVolatile(key)).Order(StringComparer.Ordinal).ToList();
        var changed = before.Tags.Keys
            .Where(key => after.Tags.TryGetValue(key, out var value) && value != before.Tags[key] && !IsVolatile(key))
            .Order(StringComparer.Ordinal)
            .ToList();
        return Result.Ok(new MetadataDiff(added, removed, changed));
    }

    public void Dispose() => _client?.Dispose();

    private static bool IsVolatile(string key)
        => VolatilePrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal));

    private static MetadataSnapshot ParseSnapshot(string pathDisplay, string json)
    {
        using var document = JsonDocument.Parse(json);
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement[0].EnumerateObject())
        {
            tags[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()!
                : property.Value.GetRawText();
        }

        return new MetadataSnapshot(pathDisplay, tags);
    }

    private static Result<T> NotAvailable<T>()
        => Result.Fail<T>(
            "EXIFTOOL_NOT_FOUND", ErrorCategory.CapabilityUnavailable, false,
            "ExifTool is not installed or not locatable; metadata operations are gated");
}
