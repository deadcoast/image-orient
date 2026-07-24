using System.Security.Cryptography;
using OrientExpress.Contracts.Models;
using OrientExpress.Infrastructure.Inference;

namespace OrientExpress.InfrastructureTests;

/// <summary>
/// PRD-FR-020 lifecycle rules: AC-038 (bad bundles quarantine and can never
/// activate) and AC-039 (atomic activation with rollback to the prior
/// verified model). Hermetic temp roots; fake artifacts with real digests.
/// </summary>
public sealed class ModelRegistryTests : IDisposable
{
    private readonly string _root =
        Directory.CreateTempSubdirectory("upright-models-tests-").FullName;

    private readonly FileModelRegistry _registry;

    public ModelRegistryTests() => _registry = new FileModelRegistry(Path.Combine(_root, "models"));

    private string WriteArtifact(string name, byte[] content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static ModelManifest Manifest(string id, string version, byte[] artifactContent, string? runtimeApi = "onnxruntime")
        => new()
        {
            SchemaVersion = "1.0",
            ModelId = id,
            Version = version,
            Task = "face-detection-five-landmarks",
            Artifact = new ModelArtifact
            {
                File = "model.onnx",
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(artifactContent)),
                Bytes = artifactContent.Length,
                Format = "onnx",
            },
            Inputs = [new TensorSpec { Name = "input" }],
            Outputs = [new TensorSpec { Name = "cls_8" }],
            Preprocessing = new PreprocessingSpec { Id = "p1", ColorOrder = "BGR", Resize = "stretch" },
            Postprocessing = new PostprocessingSpec { Id = "d1" },
            Compatibility = new CompatibilitySpec { AppVersion = ">=0.1.0", RuntimeApi = runtimeApi, AllowedProviders = ["cpu"] },
            Provenance = "test fixture",
            License = "MIT",
        };

    [Fact]
    public void Install_verify_activate_list_lifecycle_works()
    {
        byte[] content = [1, 2, 3, 4, 5];
        var artifact = WriteArtifact("a.onnx", content);

        var installed = _registry.Install(artifact, Manifest("m-one", "1.0", content));
        Assert.True(installed.IsSuccess);
        Assert.True(_registry.Verify("m-one", "1.0").IsSuccess);

        Assert.True(_registry.Activate("m-one", "1.0").IsSuccess);
        var list = _registry.List().Require();
        Assert.Single(list);
        Assert.True(list[0].IsActive);
        Assert.Equal("m-one", _registry.GetActive().Require()!.Manifest.ModelId);
    }

    [Fact]
    public void Wrong_digest_is_quarantined_and_can_never_activate()
    {
        byte[] content = [9, 9, 9];
        var artifact = WriteArtifact("bad.onnx", content);
        var manifest = Manifest("m-bad", "1.0", [1, 1, 1]);

        var installed = _registry.Install(artifact, manifest);
        Assert.False(installed.IsSuccess);
        Assert.Equal("MODEL_DIGEST_MISMATCH", installed.Error!.Code);

        // AC-038: quarantined, unlisted, unactivatable.
        Assert.Empty(_registry.List().Require());
        Assert.Equal("MODEL_NOT_INSTALLED", _registry.Activate("m-bad", "1.0").Error!.Code);
        var quarantine = Path.Combine(_root, "models", "quarantine");
        Assert.True(Directory.Exists(quarantine));
        Assert.NotEmpty(Directory.GetDirectories(quarantine));
    }

    [Fact]
    public void Incompatible_runtime_and_invalid_manifest_are_quarantined()
    {
        byte[] content = [5, 5];
        var artifact = WriteArtifact("x.onnx", content);
        Assert.Equal(
            "MODEL_MANIFEST_INVALID",
            _registry.Install(artifact, Manifest("m-x", "1.0", content, runtimeApi: "tensorrt")).Error!.Code);
        Assert.Equal(
            "MODEL_MANIFEST_INVALID",
            _registry.Install(artifact, Manifest("m-x", "1.0", content) with { Task = "not-a-task" }).Error!.Code);
    }

    [Fact]
    public void Activation_records_prior_and_rollback_restores_it()
    {
        byte[] first = [1];
        byte[] second = [2];
        _ = _registry.Install(WriteArtifact("f1.onnx", first), Manifest("m", "1.0", first));
        _ = _registry.Install(WriteArtifact("f2.onnx", second), Manifest("m", "2.0", second));

        Assert.True(_registry.Activate("m", "1.0").IsSuccess);
        Assert.True(_registry.Activate("m", "2.0").IsSuccess);
        Assert.Equal("2.0", _registry.GetActive().Require()!.Manifest.Version);

        // AC-039: rollback restores the prior verified model.
        var restored = _registry.Rollback();
        Assert.True(restored.IsSuccess);
        Assert.Equal("1.0", _registry.GetActive().Require()!.Manifest.Version);
    }

    [Fact]
    public void Active_model_cannot_be_removed_and_tamper_is_caught()
    {
        byte[] content = [7, 7, 7, 7];
        _ = _registry.Install(WriteArtifact("t.onnx", content), Manifest("m-t", "1.0", content));
        _ = _registry.Activate("m-t", "1.0");

        Assert.Equal("MODEL_ACTIVE", _registry.Remove("m-t", "1.0").Error!.Code);

        var installedArtifact = _registry.GetActive().Require()!.ArtifactPath;
        File.WriteAllBytes(installedArtifact, [0, 0, 0, 0]);
        Assert.Equal("MODEL_DIGEST_MISMATCH", _registry.Verify("m-t", "1.0").Error!.Code);
        Assert.Equal("MODEL_DIGEST_MISMATCH", _registry.Activate("m-t", "1.0").Error!.Code);
    }

    [Fact]
    public void Rollback_without_history_is_a_typed_error()
        => Assert.Equal("MODEL_NO_ROLLBACK_TARGET", _registry.Rollback().Error!.Code);

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
