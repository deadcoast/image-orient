using System.Globalization;
using System.Text.RegularExpressions;
using OrientExpress.Contracts.Config;

namespace OrientExpress.Application.Config;

/// <summary>
/// Mirrors the bounds, enums, consts, and patterns of
/// <c>schemas/config.schema.json</c> as executable checks (PRD-FR-019):
/// invalid configuration fails before any work begins. Keep in sync with the
/// schema; the schema file remains the published contract.
/// </summary>
public static partial class ConfigValidator
{
    public const string CodeInvalidValue = "CONFIG_INVALID_VALUE";
    public const string CodeOutOfRange = "CONFIG_OUT_OF_RANGE";
    public const string CodeSchemaVersion = "CONFIG_SCHEMA_VERSION";
    public const string CodeTelemetryForbidden = "CONFIG_TELEMETRY_FORBIDDEN";
    public const string CodeEmptyValue = "CONFIG_EMPTY_VALUE";

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex FaceModelPattern();

    public static IReadOnlyList<ConfigIssue> Validate(UprightConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var issues = new List<ConfigIssue>();

        if (config.SchemaVersion != "1.0")
        {
            issues.Add(new ConfigIssue(
                "schemaVersion", CodeSchemaVersion,
                $"schemaVersion must be '1.0', not '{config.SchemaVersion}'"));
        }

        RequireNonEmpty(issues, "paths.jobRoot", config.Paths?.JobRoot);
        RequireNonEmpty(issues, "paths.modelRoot", config.Paths?.ModelRoot);
        RequireNonEmpty(issues, "paths.cacheRoot", config.Paths?.CacheRoot);
        if (config.Paths?.BackupRoot is { } backupRoot && backupRoot.Length == 0)
        {
            issues.Add(new ConfigIssue("paths.backupRoot", CodeEmptyValue, "backupRoot may be null but not empty"));
        }

        RequireEnum(issues, "analysis.profile", config.Analysis?.Profile, ["conservative", "balanced", "expert"]);
        RequireRange(issues, "analysis.maxThumbnailLongEdge", config.Analysis?.MaxThumbnailLongEdge, 320, 4096);
        if (config.Analysis?.FaceModel is { } faceModel && !FaceModelPattern().IsMatch(faceModel))
        {
            issues.Add(new ConfigIssue(
                "analysis.faceModel", CodeInvalidValue,
                "faceModel may contain only letters, digits, '.', '_', and '-'"));
        }

        RequireEnum(issues, "output.mode", config.Output?.Mode, ["safe-copy", "in-place"]);
        RequireEnum(issues, "output.collision", config.Output?.Collision, ["error", "skip", "rename"]);
        RequireEnum(issues, "output.jpegEdge", config.Output?.JpegEdge, ["abort", "preserve-partial", "trim", "reencode"]);
        RequireEnum(
            issues, "output.metadataProfile", config.Output?.MetadataProfile,
            ["preserve-personal", "preserve-minimal", "privacy-strip"]);

        RequireRange(issues, "resources.memoryMiB", config.Resources?.MemoryMiB, 256, 65536);
        RequireRange(issues, "resources.metadataWorkers", config.Resources?.MetadataWorkers, 1, 16);
        RequireRange(issues, "resources.decodeWorkers", config.Resources?.DecodeWorkers, 1, 32);
        RequireRange(issues, "resources.inferenceWorkers", config.Resources?.InferenceWorkers, 1, 16);
        RequireRange(issues, "resources.transformWorkers", config.Resources?.TransformWorkers, 1, 32);
        RequireRange(issues, "resources.verificationWorkers", config.Resources?.VerificationWorkers, 1, 32);
        RequireRange(issues, "resources.queueCapacity", config.Resources?.QueueCapacity, 1, 4096);

        RequireRange(issues, "discovery.maxFiles", config.Discovery?.MaxFiles, 1, long.MaxValue);
        RequireRange(issues, "discovery.maxBytes", config.Discovery?.MaxBytes, 1, long.MaxValue);

        if (config.Privacy?.Telemetry == true)
        {
            // Schema const: no layer may ever enable telemetry (NFR-PRIV).
            issues.Add(new ConfigIssue(
                "privacy.telemetry", CodeTelemetryForbidden, "telemetry cannot be enabled; the schema pins it to false"));
        }

        RequireRange(issues, "privacy.diagnosticRetentionDays", config.Privacy?.DiagnosticRetentionDays, 1, 365);
        return issues;
    }

    private static void RequireNonEmpty(List<ConfigIssue> issues, string path, string? value)
    {
        if (value is not null && value.Trim().Length == 0)
        {
            issues.Add(new ConfigIssue(path, CodeEmptyValue, path + " must not be empty"));
        }
    }

    private static void RequireEnum(List<ConfigIssue> issues, string path, string? value, string[] allowed)
    {
        if (value is not null && !allowed.Contains(value, StringComparer.Ordinal))
        {
            issues.Add(new ConfigIssue(
                path, CodeInvalidValue,
                $"{path} must be one of: {string.Join(", ", allowed)}; got '{value}'"));
        }
    }

    private static void RequireRange(List<ConfigIssue> issues, string path, long? value, long minimum, long maximum)
    {
        if (value is { } number && (number < minimum || number > maximum))
        {
            issues.Add(new ConfigIssue(
                path, CodeOutOfRange,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{path} must be between {minimum} and {maximum}; got {number}")));
        }
    }
}
