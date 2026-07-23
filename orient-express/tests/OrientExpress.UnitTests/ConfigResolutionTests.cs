using OrientExpress.Application.Config;
using OrientExpress.Contracts.Config;

namespace OrientExpress.UnitTests;

/// <summary>
/// AC-036 (precedence) and the DOC-CONFIG validation semantics as pure
/// Application-layer tests.
/// </summary>
public sealed class ConfigResolutionTests
{
    private static UprightConfig Defaults => ConfigDefaults.Create(@"C:\data\orient-express");

    private static (UprightConfig Merged, IReadOnlyDictionary<string, string> Provenance) Merge(
        UprightConfig? cli = null,
        UprightConfig? environment = null,
        UprightConfig? explicitFile = null,
        UprightConfig? userFile = null)
        => ConfigMerger.Merge(
        [
            new ConfigLayer(ConfigLayer.Cli, cli),
            new ConfigLayer(ConfigLayer.Environment, environment),
            new ConfigLayer(ConfigLayer.ExplicitFile, explicitFile),
            new ConfigLayer(ConfigLayer.UserFile, userFile),
            new ConfigLayer(ConfigLayer.Defaults, Defaults),
        ]);

    private static UprightConfig WithProfile(string profile)
        => new() { Analysis = new AnalysisConfig { Profile = profile } };

    [Fact]
    public void Defaults_pass_validation_completely()
    {
        var (merged, provenance) = Merge();
        Assert.Empty(ConfigValidator.Validate(merged));
        Assert.All(provenance.Values, source => Assert.Equal(ConfigLayer.Defaults, source));
    }

    [Fact]
    public void Precedence_is_cli_env_explicit_user_defaults()
    {
        var (merged, provenance) = Merge(
            cli: WithProfile("expert"),
            environment: WithProfile("balanced"),
            explicitFile: WithProfile("conservative"),
            userFile: WithProfile("balanced"));
        Assert.Equal("expert", merged.Analysis?.Profile);
        Assert.Equal(ConfigLayer.Cli, provenance["analysis.profile"]);

        var (envWins, envProvenance) = Merge(
            environment: WithProfile("balanced"),
            explicitFile: WithProfile("expert"));
        Assert.Equal("balanced", envWins.Analysis?.Profile);
        Assert.Equal(ConfigLayer.Environment, envProvenance["analysis.profile"]);

        var (explicitWins, explicitProvenance) = Merge(
            explicitFile: WithProfile("expert"),
            userFile: WithProfile("balanced"));
        Assert.Equal("expert", explicitWins.Analysis?.Profile);
        Assert.Equal(ConfigLayer.ExplicitFile, explicitProvenance["analysis.profile"]);
    }

    [Fact]
    public void Unrelated_settings_from_lower_layers_survive_a_higher_overlay()
    {
        var (merged, provenance) = Merge(
            userFile: new UprightConfig { Output = new OutputConfig { Collision = "rename" } },
            environment: WithProfile("balanced"));
        Assert.Equal("rename", merged.Output?.Collision);
        Assert.Equal(ConfigLayer.UserFile, provenance["output.collision"]);
        Assert.Equal("error", Defaults.Output?.Collision);
        Assert.Equal("safe-copy", merged.Output?.Mode);
    }

    [Fact]
    public void Lists_replace_wholesale_rather_than_concatenate()
    {
        var (merged, _) = Merge(
            userFile: new UprightConfig
            {
                Discovery = new DiscoveryConfig { Include = ["**/*.heic"] },
            });
        Assert.Equal(["**/*.heic"], merged.Discovery?.Include);
    }

    [Theory]
    [InlineData("analysis.profile", "cinematic", ConfigValidator.CodeInvalidValue)]
    [InlineData("output.mode", "overwrite", ConfigValidator.CodeInvalidValue)]
    [InlineData("output.jpegEdge", "crop", ConfigValidator.CodeInvalidValue)]
    public void Enum_violations_are_reported_with_stable_codes(string path, string value, string code)
    {
        var overlay = path switch
        {
            "analysis.profile" => WithProfile(value),
            "output.mode" => new UprightConfig { Output = new OutputConfig { Mode = value } },
            _ => new UprightConfig { Output = new OutputConfig { JpegEdge = value } },
        };
        var (merged, _) = Merge(userFile: overlay);
        var issue = Assert.Single(ConfigValidator.Validate(merged));
        Assert.Equal(path, issue.Path);
        Assert.Equal(code, issue.Code);
    }

    [Theory]
    [InlineData(255)]
    [InlineData(65537)]
    public void Range_violations_are_reported(int memory)
    {
        var (merged, _) = Merge(
            userFile: new UprightConfig { Resources = new ResourcesConfig { MemoryMiB = memory } });
        var issue = Assert.Single(ConfigValidator.Validate(merged));
        Assert.Equal("resources.memoryMiB", issue.Path);
        Assert.Equal(ConfigValidator.CodeOutOfRange, issue.Code);
    }

    [Fact]
    public void Telemetry_cannot_be_enabled_by_any_layer()
    {
        var (merged, _) = Merge(
            cli: new UprightConfig { Privacy = new PrivacyConfig { Telemetry = true } });
        var issue = Assert.Single(ConfigValidator.Validate(merged));
        Assert.Equal(ConfigValidator.CodeTelemetryForbidden, issue.Code);
    }

    [Fact]
    public void Wrong_schema_version_is_rejected()
    {
        var (merged, _) = Merge(userFile: new UprightConfig { SchemaVersion = "2.0" });
        Assert.Contains(
            ConfigValidator.Validate(merged),
            issue => issue.Code == ConfigValidator.CodeSchemaVersion);
    }

    [Fact]
    public void Environment_variables_map_and_fail_loudly()
    {
        var (overlay, issues) = EnvironmentConfigParser.Parse(new Dictionary<string, string>
        {
            ["UPRIGHT_OUTPUT__MODE"] = "in-place",
            ["UPRIGHT_RESOURCES__MEMORYMIB"] = "2048",
            ["UPRIGHT_DISCOVERY__INCLUDE"] = "**/*.jpg;**/*.png",
            ["UNRELATED"] = "ignored",
        });
        Assert.Empty(issues);
        Assert.Equal("in-place", overlay?.Output?.Mode);
        Assert.Equal(2048, overlay?.Resources?.MemoryMiB);
        Assert.Equal(["**/*.jpg", "**/*.png"], overlay?.Discovery?.Include);

        var (_, unknownIssues) = EnvironmentConfigParser.Parse(new Dictionary<string, string>
        {
            ["UPRIGHT_OUTPUT__MODES"] = "typo",
        });
        Assert.Equal(EnvironmentConfigParser.CodeUnknownVariable, Assert.Single(unknownIssues).Code);

        var (_, valueIssues) = EnvironmentConfigParser.Parse(new Dictionary<string, string>
        {
            ["UPRIGHT_RESOURCES__MEMORYMIB"] = "lots",
        });
        Assert.Equal(EnvironmentConfigParser.CodeInvalidValue, Assert.Single(valueIssues).Code);
    }

    [Fact]
    public void Digest_is_stable_and_sensitive_to_values()
    {
        var (merged, _) = Merge();
        Assert.Equal(ConfigDigest.Compute(merged), ConfigDigest.Compute(merged));

        var (changed, _) = Merge(userFile: WithProfile("balanced"));
        Assert.NotEqual(ConfigDigest.Compute(merged), ConfigDigest.Compute(changed));
    }
}
