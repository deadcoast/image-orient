using System.Security.Cryptography;
using System.Text.Json;
using OrientExpress.Cli.Hosting;
using OrientExpress.Contracts.Cli;

namespace OrientExpress.CliTests;

/// <summary>
/// `upright models …` lifecycle golden tests (PRD-FR-020) with a hermetic
/// HostEnvironment: the model root derives from the injected DataRoot.
/// </summary>
public sealed class ModelsCommandTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-models-cli-tests-").FullName;

    private HostEnvironment Environment()
        => new(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Path.Combine(_directory, "user", "config.json"),
            Path.Combine(_directory, "data"));

    private static (int ExitCode, string Stdout, string Stderr) Invoke(HostEnvironment environment, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliHost.Invoke(args, stdout, stderr, hostEnvironment: environment);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private (string ArtifactPath, string ManifestPath) WriteBundle(string id, string version, byte[] content, string? sha256 = null)
    {
        var artifactPath = Path.Combine(_directory, $"{id}-{version}.onnx");
        File.WriteAllBytes(artifactPath, content);
        var manifest = new
        {
            schemaVersion = "1.0",
            modelId = id,
            version,
            task = "face-detection-five-landmarks",
            artifact = new
            {
                file = "model.onnx",
                sha256 = sha256 ?? Convert.ToHexStringLower(SHA256.HashData(content)),
                bytes = content.Length,
                format = "onnx",
            },
            inputs = new[] { new { name = "input" } },
            outputs = new[] { new { name = "cls_8" } },
            preprocessing = new { id = "p1", colorOrder = "BGR", resize = "stretch" },
            postprocessing = new { id = "d1" },
            compatibility = new { appVersion = ">=0.1.0", runtimeApi = "onnxruntime", allowedProviders = new[] { "cpu" } },
            provenance = "test fixture",
            license = "MIT",
        };
        var manifestPath = Path.Combine(_directory, $"{id}-{version}.manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
        return (artifactPath, manifestPath);
    }

    [Fact]
    public void Full_lifecycle_install_activate_rollback_remove()
    {
        var environment = Environment();
        var (artifact1, manifest1) = WriteBundle("m", "1.0", [1, 2, 3]);
        var (artifact2, manifest2) = WriteBundle("m", "2.0", [4, 5, 6]);

        Assert.Equal(0, Invoke(environment, "models", "install", artifact1, "--manifest", manifest1).ExitCode);
        Assert.Equal(0, Invoke(environment, "models", "install", artifact2, "--manifest", manifest2).ExitCode);
        Assert.Equal(0, Invoke(environment, "models", "activate", "m", "1.0").ExitCode);
        Assert.Equal(0, Invoke(environment, "models", "activate", "m", "2.0").ExitCode);

        var list = Invoke(environment, "models", "list", "--json");
        using (var document = JsonDocument.Parse(list.Stdout))
        {
            var models = document.RootElement.GetProperty("models").EnumerateArray().ToList();
            Assert.Equal(2, models.Count);
            Assert.True(models.Single(m => m.GetProperty("version").GetString() == "2.0")
                .GetProperty("active").GetBoolean());
        }

        // The active model refuses removal (CONFLICT).
        Assert.Equal((int)CliExitCode.Conflict, Invoke(environment, "models", "remove", "m", "2.0").ExitCode);

        Assert.Equal(0, Invoke(environment, "models", "rollback").ExitCode);
        Assert.Equal(0, Invoke(environment, "models", "remove", "m", "2.0").ExitCode);
        Assert.Equal(0, Invoke(environment, "models", "verify").ExitCode);
    }

    [Fact]
    public void Digest_mismatch_quarantines_with_verification_failed_exit()
    {
        var environment = Environment();
        var (artifact, manifest) = WriteBundle("bad", "1.0", [1, 2, 3], sha256: new string('a', 64));

        var result = Invoke(environment, "models", "install", artifact, "--manifest", manifest);
        Assert.Equal((int)CliExitCode.VerificationFailed, result.ExitCode);
        Assert.Contains("MODEL_DIGEST_MISMATCH", result.Stderr, StringComparison.Ordinal);
        Assert.Contains("quarantined", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_model_and_missing_rollback_are_typed_argument_errors()
    {
        var environment = Environment();
        Assert.Equal((int)CliExitCode.InvalidArgument, Invoke(environment, "models", "activate", "nope", "1.0").ExitCode);
        Assert.Equal((int)CliExitCode.InvalidArgument, Invoke(environment, "models", "rollback").ExitCode);
    }

    [Fact]
    public void Doctor_reports_the_active_model_after_activation()
    {
        var environment = Environment();
        var (artifact, manifest) = WriteBundle("doc-model", "1.0", [8, 8, 8]);
        _ = Invoke(environment, "models", "install", artifact, "--manifest", manifest);
        _ = Invoke(environment, "models", "activate", "doc-model", "1.0");

        var doctor = Invoke(environment, "doctor", "--json", "--non-interactive");
        using var document = JsonDocument.Parse(doctor.Stdout);
        var component = document.RootElement.GetProperty("components").EnumerateArray()
            .Single(c => c.GetProperty("id").GetString() == "orientation-model");
        Assert.True(component.GetProperty("available").GetBoolean());
        Assert.Contains("doc-model", component.GetProperty("status").GetString(), StringComparison.Ordinal);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
