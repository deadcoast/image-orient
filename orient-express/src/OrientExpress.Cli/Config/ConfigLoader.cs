using System.Text.Json;
using OrientExpress.Application.Config;
using OrientExpress.Cli.Hosting;
using OrientExpress.Contracts.Config;

namespace OrientExpress.Cli.Config;

public sealed record ConfigLoadResult(EffectiveConfig? Effective, IReadOnlyList<ConfigIssue> Issues)
{
    public bool Succeeded => Effective is not null && Issues.Count == 0;
}

/// <summary>
/// Composition-root configuration assembly (PRD-FR-019): reads the file and
/// environment layers, delegates precedence/validation/digest to the pure
/// Application logic, and fails closed — any parse, mapping, or validation
/// issue means no effective configuration and no work (AC-037).
/// </summary>
public static class ConfigLoader
{
    public const string CodeParseError = "CONFIG_PARSE_ERROR";
    public const string CodeFileNotFound = "CONFIG_FILE_NOT_FOUND";

    public static ConfigLoadResult Load(HostEnvironment environment, string? explicitConfigPath, string? cliProfile)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var issues = new List<ConfigIssue>();

        var userLayer = ReadFileLayer(environment.UserConfigPath, required: false, issues, "user-file");
        UprightConfig? explicitLayer = null;
        if (explicitConfigPath is not null)
        {
            explicitLayer = ReadFileLayer(explicitConfigPath, required: true, issues, "explicit-file");
        }

        var (environmentLayer, environmentIssues) = EnvironmentConfigParser.Parse(environment.EnvironmentVariables);
        issues.AddRange(environmentIssues);

        UprightConfig? cliLayer = null;
        if (cliProfile is not null)
        {
            cliLayer = new UprightConfig { Analysis = new AnalysisConfig { Profile = cliProfile } };
        }

        if (issues.Count > 0)
        {
            return new ConfigLoadResult(null, issues);
        }

        var (merged, provenance) = ConfigMerger.Merge(
        [
            new ConfigLayer(ConfigLayer.Cli, cliLayer),
            new ConfigLayer(ConfigLayer.Environment, environmentLayer),
            new ConfigLayer(ConfigLayer.ExplicitFile, explicitLayer),
            new ConfigLayer(ConfigLayer.UserFile, userLayer),
            new ConfigLayer(ConfigLayer.Defaults, ConfigDefaults.Create(environment.DataRoot)),
        ]);

        var validation = ConfigValidator.Validate(merged);
        if (validation.Count > 0)
        {
            return new ConfigLoadResult(null, validation);
        }

        return new ConfigLoadResult(
            new EffectiveConfig(merged, provenance, ConfigDigest.Compute(merged)), []);
    }

    private static UprightConfig? ReadFileLayer(
        string path, bool required, List<ConfigIssue> issues, string layerName)
    {
        if (!File.Exists(path))
        {
            if (required)
            {
                issues.Add(new ConfigIssue(layerName, CodeFileNotFound, $"configuration file not found: {path}"));
            }

            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UprightConfig>(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            // Includes unknown-member rejection (AC-037): typos fail loudly.
            issues.Add(new ConfigIssue(layerName, CodeParseError, $"{path}: {exception.Message}"));
            return null;
        }
    }
}
