using System.Globalization;
using OrientExpress.Contracts.Config;

namespace OrientExpress.Application.Config;

/// <summary>
/// Maps `UPRIGHT_GROUP__KEY` environment variables onto the configuration
/// tree (DOC-CONFIG). Unknown `UPRIGHT_` names and malformed values are
/// issues, not silent ignores (AC-037: typos fail before work begins).
/// List values use ';' separators.
/// </summary>
public static class EnvironmentConfigParser
{
    public const string Prefix = "UPRIGHT_";
    public const string CodeUnknownVariable = "CONFIG_UNKNOWN_ENV_VARIABLE";
    public const string CodeInvalidValue = "CONFIG_INVALID_ENV_VALUE";

    public static (UprightConfig? Overlay, IReadOnlyList<ConfigIssue> Issues) Parse(
        IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var issues = new List<ConfigIssue>();
        var overlay = new UprightConfig();
        var any = false;

        foreach (var (rawName, rawValue) in environment.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!rawName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = rawName[Prefix.Length..].ToUpperInvariant();
            var applied = Apply(ref overlay, key, rawValue, issues, rawName);
            if (applied is null)
            {
                issues.Add(new ConfigIssue(
                    rawName, CodeUnknownVariable,
                    $"environment variable '{rawName}' does not map to a configuration setting"));
            }
            else
            {
                any |= applied.Value;
            }
        }

        return (any ? overlay : null, issues);
    }

    /// <summary>Returns null for unknown keys, false when the value failed to parse, true when applied.</summary>
    private static bool? Apply(
        ref UprightConfig overlay, string key, string value, List<ConfigIssue> issues, string variableName)
    {
        switch (key)
        {
            case "PATHS__JOBROOT":
                overlay = overlay with { Paths = (overlay.Paths ?? new PathsConfig()) with { JobRoot = value } };
                return true;
            case "PATHS__MODELROOT":
                overlay = overlay with { Paths = (overlay.Paths ?? new PathsConfig()) with { ModelRoot = value } };
                return true;
            case "PATHS__CACHEROOT":
                overlay = overlay with { Paths = (overlay.Paths ?? new PathsConfig()) with { CacheRoot = value } };
                return true;
            case "PATHS__BACKUPROOT":
                overlay = overlay with { Paths = (overlay.Paths ?? new PathsConfig()) with { BackupRoot = value } };
                return true;

            case "DISCOVERY__RECURSE":
            case "DISCOVERY__FOLLOWREPARSEPOINTS":
                {
                    if (!TryParseBool(value, out var parsed))
                    {
                        return Invalid(issues, variableName, value, "a boolean (true/false)");
                    }

                    var discovery = overlay.Discovery ?? new DiscoveryConfig();
                    overlay = overlay with
                    {
                        Discovery = key == "DISCOVERY__RECURSE"
                            ? discovery with { Recurse = parsed }
                            : discovery with { FollowReparsePoints = parsed },
                    };
                    return true;
                }

            case "DISCOVERY__INCLUDE":
                overlay = overlay with
                {
                    Discovery = (overlay.Discovery ?? new DiscoveryConfig()) with { Include = SplitList(value) },
                };
                return true;
            case "DISCOVERY__EXCLUDE":
                overlay = overlay with
                {
                    Discovery = (overlay.Discovery ?? new DiscoveryConfig()) with { Exclude = SplitList(value) },
                };
                return true;
            case "DISCOVERY__MAXFILES":
            case "DISCOVERY__MAXBYTES":
                {
                    if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return Invalid(issues, variableName, value, "a positive integer");
                    }

                    var discovery = overlay.Discovery ?? new DiscoveryConfig();
                    overlay = overlay with
                    {
                        Discovery = key == "DISCOVERY__MAXFILES"
                            ? discovery with { MaxFiles = parsed }
                            : discovery with { MaxBytes = parsed },
                    };
                    return true;
                }

            case "ANALYSIS__PROFILE":
                overlay = overlay with
                {
                    Analysis = (overlay.Analysis ?? new AnalysisConfig()) with { Profile = value },
                };
                return true;
            case "ANALYSIS__FACEMODEL":
                overlay = overlay with
                {
                    Analysis = (overlay.Analysis ?? new AnalysisConfig()) with { FaceModel = value },
                };
                return true;
            case "ANALYSIS__MAXTHUMBNAILLONGEDGE":
                return ApplyInt(
                    ref overlay, value, issues, variableName,
                    static (config, parsed) => config with
                    {
                        Analysis = (config.Analysis ?? new AnalysisConfig()) with { MaxThumbnailLongEdge = parsed },
                    });

            case "OUTPUT__MODE":
                overlay = overlay with { Output = (overlay.Output ?? new OutputConfig()) with { Mode = value } };
                return true;
            case "OUTPUT__COLLISION":
                overlay = overlay with { Output = (overlay.Output ?? new OutputConfig()) with { Collision = value } };
                return true;
            case "OUTPUT__JPEGEDGE":
                overlay = overlay with { Output = (overlay.Output ?? new OutputConfig()) with { JpegEdge = value } };
                return true;
            case "OUTPUT__METADATAPROFILE":
                overlay = overlay with
                {
                    Output = (overlay.Output ?? new OutputConfig()) with { MetadataProfile = value },
                };
                return true;

            case "RESOURCES__MEMORYMIB":
                return ApplyInt(
                    ref overlay, value, issues, variableName,
                    static (config, parsed) => config with
                    {
                        Resources = (config.Resources ?? new ResourcesConfig()) with { MemoryMiB = parsed },
                    });
            case "RESOURCES__METADATAWORKERS":
                return ApplyInt(
                    ref overlay, value, issues, variableName,
                    static (config, parsed) => config with
                    {
                        Resources = (config.Resources ?? new ResourcesConfig()) with { MetadataWorkers = parsed },
                    });
            case "RESOURCES__INFERENCEWORKERS":
                return ApplyInt(
                    ref overlay, value, issues, variableName,
                    static (config, parsed) => config with
                    {
                        Resources = (config.Resources ?? new ResourcesConfig()) with { InferenceWorkers = parsed },
                    });
            case "RESOURCES__TRANSFORMWORKERS":
                return ApplyInt(
                    ref overlay, value, issues, variableName,
                    static (config, parsed) => config with
                    {
                        Resources = (config.Resources ?? new ResourcesConfig()) with { TransformWorkers = parsed },
                    });

            case "PRIVACY__REVIEWTHUMBNAILS":
            case "PRIVACY__REDACTPATHSINDIAGNOSTICS":
            case "PRIVACY__TELEMETRY":
                {
                    if (!TryParseBool(value, out var parsed))
                    {
                        return Invalid(issues, variableName, value, "a boolean (true/false)");
                    }

                    var privacy = overlay.Privacy ?? new PrivacyConfig();
                    overlay = overlay with
                    {
                        Privacy = key switch
                        {
                            "PRIVACY__REVIEWTHUMBNAILS" => privacy with { ReviewThumbnails = parsed },
                            "PRIVACY__REDACTPATHSINDIAGNOSTICS" => privacy with { RedactPathsInDiagnostics = parsed },
                            _ => privacy with { Telemetry = parsed },
                        },
                    };
                    return true;
                }

            default:
                return null;
        }
    }

    private static bool? ApplyInt(
        ref UprightConfig overlay,
        string value,
        List<ConfigIssue> issues,
        string variableName,
        Func<UprightConfig, int, UprightConfig> apply)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return Invalid(issues, variableName, value, "an integer");
        }

        overlay = apply(overlay, parsed);
        return true;
    }

    private static bool Invalid(List<ConfigIssue> issues, string variableName, string value, string expected)
    {
        issues.Add(new ConfigIssue(
            variableName, CodeInvalidValue, $"'{value}' is not {expected}"));
        return false;
    }

    private static bool TryParseBool(string value, out bool parsed)
    {
        parsed = false;
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            parsed = true;
            return true;
        }

        return string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> SplitList(string value)
        => [.. value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
