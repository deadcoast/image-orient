using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using OrientExpress.Application.Inference;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Models;

namespace OrientExpress.Infrastructure.Inference;

/// <summary>
/// File-based model registry under the configured model root (PRD-FR-020):
/// bundles live at <c>&lt;root&gt;/&lt;modelId&gt;/&lt;version&gt;/</c> with their manifest;
/// failures quarantine under <c>&lt;root&gt;/quarantine/</c> and can never be
/// activated (AC-038); the active pointer is written atomically with the
/// prior model recorded for rollback (AC-039).
/// </summary>
public sealed class FileModelRegistry(string modelRoot) : IModelRegistry
{
    public const string SupportedRuntimeApi = "onnxruntime";
    private const string ManifestFileName = "manifest.json";
    private const string ActivePointerFileName = "active.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string ModelRoot { get; } = modelRoot;

    public Result<IReadOnlyList<InstalledModel>> List()
    {
        try
        {
            var models = new List<InstalledModel>();
            if (!Directory.Exists(ModelRoot))
            {
                return Result.Ok<IReadOnlyList<InstalledModel>>(models);
            }

            var active = ReadPointer();
            foreach (var modelDirectory in Directory.GetDirectories(ModelRoot))
            {
                if (Path.GetFileName(modelDirectory) is "quarantine")
                {
                    continue;
                }

                foreach (var versionDirectory in Directory.GetDirectories(modelDirectory))
                {
                    var manifestPath = Path.Combine(versionDirectory, ManifestFileName);
                    if (!File.Exists(manifestPath))
                    {
                        continue;
                    }

                    var manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(manifestPath));
                    if (manifest?.ModelId is null || manifest.Version is null || manifest.Artifact?.File is null)
                    {
                        continue;
                    }

                    models.Add(new InstalledModel(
                        manifest,
                        versionDirectory,
                        Path.Combine(versionDirectory, manifest.Artifact.File),
                        IsActive: active is not null
                            && active.Value.ModelId == manifest.ModelId
                            && active.Value.Version == manifest.Version));
                }
            }

            return Result.Ok<IReadOnlyList<InstalledModel>>(
                [.. models.OrderBy(m => m.Manifest.ModelId, StringComparer.Ordinal)
                    .ThenBy(m => m.Manifest.Version, StringComparer.Ordinal)]);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return Result.Fail<IReadOnlyList<InstalledModel>>(
                "MODEL_REGISTRY_UNREADABLE", ErrorCategory.Unexpected, true, exception.Message);
        }
    }

    public Result<InstalledModel> Install(string sourceArtifactPath, ModelManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (!File.Exists(sourceArtifactPath))
        {
            return Result.Fail<InstalledModel>(
                "MODEL_ARTIFACT_NOT_FOUND", ErrorCategory.NotFound, false,
                $"no artifact at '{sourceArtifactPath}'");
        }

        var validation = ManifestValidator.Validate(manifest);
        if (validation is not null)
        {
            return Quarantine(sourceArtifactPath, manifest, "MODEL_MANIFEST_INVALID", validation);
        }

        string digest;
        using (var stream = File.OpenRead(sourceArtifactPath))
        {
            digest = Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        if (digest != manifest.Artifact!.Sha256)
        {
            return Quarantine(
                sourceArtifactPath, manifest, "MODEL_DIGEST_MISMATCH",
                $"artifact sha256 {digest} does not match manifest {manifest.Artifact.Sha256}");
        }

        var directory = Path.Combine(ModelRoot, manifest.ModelId!, manifest.Version!);
        if (Directory.Exists(directory))
        {
            return Result.Fail<InstalledModel>(
                "MODEL_ALREADY_INSTALLED", ErrorCategory.IntegrityViolation, false,
                $"{manifest.ModelId} {manifest.Version} is already installed; remove it first");
        }

        Directory.CreateDirectory(directory);
        var artifactPath = Path.Combine(directory, manifest.Artifact.File!);
        File.Copy(sourceArtifactPath, artifactPath);
        File.WriteAllText(Path.Combine(directory, ManifestFileName), JsonSerializer.Serialize(manifest, JsonOptions));
        return Result.Ok(new InstalledModel(manifest, directory, artifactPath, IsActive: false));
    }

    public Result<InstalledModel> Verify(string modelId, string version)
    {
        var found = Find(modelId, version);
        if (!found.IsSuccess)
        {
            return found;
        }

        var model = found.Require();
        using var stream = File.OpenRead(model.ArtifactPath);
        var digest = Convert.ToHexStringLower(SHA256.HashData(stream));
        return digest == model.Manifest.Artifact!.Sha256
            ? Result.Ok(model)
            : Result.Fail<InstalledModel>(
                "MODEL_DIGEST_MISMATCH", ErrorCategory.IntegrityViolation, false,
                $"installed artifact for {modelId} {version} no longer matches its manifest digest");
    }

    public Result<InstalledModel> Activate(string modelId, string version)
    {
        // AC-038: only a bundle that verifies right now may become active.
        var verified = Verify(modelId, version);
        if (!verified.IsSuccess)
        {
            return verified;
        }

        var current = ReadPointer();
        WritePointerAtomically(new ActivePointer(
            modelId, version, current?.ModelId, current?.Version));
        return Result.Ok(verified.Require() with { IsActive = true });
    }

    public Result<InstalledModel> Rollback()
    {
        var current = ReadPointer();
        if (current?.PreviousModelId is null || current.Value.PreviousVersion is null)
        {
            return Result.Fail<InstalledModel>(
                "MODEL_NO_ROLLBACK_TARGET", ErrorCategory.NotFound, false,
                "no prior activated model is recorded");
        }

        // AC-039: rollback restores the prior model only if it still verifies.
        var verified = Verify(current.Value.PreviousModelId, current.Value.PreviousVersion);
        if (!verified.IsSuccess)
        {
            return verified;
        }

        WritePointerAtomically(new ActivePointer(
            current.Value.PreviousModelId, current.Value.PreviousVersion,
            current.Value.ModelId, current.Value.Version));
        return Result.Ok(verified.Require() with { IsActive = true });
    }

    public Result<bool> Remove(string modelId, string version)
    {
        var active = ReadPointer();
        if (active is not null && active.Value.ModelId == modelId && active.Value.Version == version)
        {
            return Result.Fail<bool>(
                "MODEL_ACTIVE", ErrorCategory.IntegrityViolation, false,
                "the active model cannot be removed; activate or rollback to another model first");
        }

        var found = Find(modelId, version);
        if (!found.IsSuccess)
        {
            return Result.Fail<bool>(found.Error!);
        }

        Directory.Delete(found.Require().Directory, recursive: true);
        return Result.Ok(true);
    }

    public Result<InstalledModel?> GetActive()
    {
        var pointer = ReadPointer();
        if (pointer is null)
        {
            return Result.Ok<InstalledModel?>(null);
        }

        var found = Find(pointer.Value.ModelId, pointer.Value.Version);
        return found.IsSuccess
            ? Result.Ok<InstalledModel?>(found.Require() with { IsActive = true })
            : Result.Fail<InstalledModel?>(found.Error!);
    }

    private Result<InstalledModel> Find(string modelId, string version)
    {
        var directory = Path.Combine(ModelRoot, modelId, version);
        var manifestPath = Path.Combine(directory, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            return Result.Fail<InstalledModel>(
                "MODEL_NOT_INSTALLED", ErrorCategory.NotFound, false,
                $"{modelId} {version} is not installed");
        }

        var manifest = JsonSerializer.Deserialize<ModelManifest>(File.ReadAllText(manifestPath));
        if (manifest?.Artifact?.File is null)
        {
            return Result.Fail<InstalledModel>(
                "MODEL_MANIFEST_INVALID", ErrorCategory.IntegrityViolation, false,
                $"installed manifest for {modelId} {version} is unreadable");
        }

        var pointer = ReadPointer();
        return Result.Ok(new InstalledModel(
            manifest,
            directory,
            Path.Combine(directory, manifest.Artifact.File),
            IsActive: pointer is not null && pointer.Value.ModelId == modelId && pointer.Value.Version == version));
    }

    private Result<InstalledModel> Quarantine(
        string sourceArtifactPath, ModelManifest manifest, string code, string reason)
    {
        var quarantineDirectory = Path.Combine(
            ModelRoot, "quarantine",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{manifest.ModelId ?? "unknown"}-{manifest.Version ?? "unknown"}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}"));
        Directory.CreateDirectory(quarantineDirectory);
        File.Copy(sourceArtifactPath, Path.Combine(quarantineDirectory, Path.GetFileName(sourceArtifactPath)));
        File.WriteAllText(
            Path.Combine(quarantineDirectory, "quarantine-reason.txt"),
            code + ": " + reason + Environment.NewLine);
        File.WriteAllText(
            Path.Combine(quarantineDirectory, ManifestFileName),
            JsonSerializer.Serialize(manifest, JsonOptions));
        return Result.Fail<InstalledModel>(
            code, ErrorCategory.IntegrityViolation, false,
            reason + $"; bundle quarantined at '{quarantineDirectory}'");
    }

    private readonly record struct ActivePointer(
        string ModelId, string Version, string? PreviousModelId, string? PreviousVersion);

    private ActivePointer? ReadPointer()
    {
        var path = Path.Combine(ModelRoot, ActivePointerFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ActivePointer>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void WritePointerAtomically(ActivePointer pointer)
    {
        Directory.CreateDirectory(ModelRoot);
        var path = Path.Combine(ModelRoot, ActivePointerFileName);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(pointer, JsonOptions));
        if (File.Exists(path))
        {
            // File.Replace maps to the SPIKE-006-qualified ReplaceFileW.
            File.Replace(temporary, path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporary, path);
        }
    }
}

/// <summary>Code-side enforcement of the manifest schema's required fields and enums.</summary>
public static class ManifestValidator
{
    private static readonly string[] Tasks = ["face-detection-five-landmarks", "scene-orientation-four-way"];

    /// <summary>Returns null when valid, otherwise the first violation.</summary>
    public static string? Validate(ModelManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != "1.0")
        {
            return $"schemaVersion must be '1.0', not '{manifest.SchemaVersion}'";
        }

        if (string.IsNullOrWhiteSpace(manifest.ModelId) || string.IsNullOrWhiteSpace(manifest.Version))
        {
            return "modelId and version are required";
        }

        if (manifest.Task is null || !Tasks.Contains(manifest.Task, StringComparer.Ordinal))
        {
            return $"task must be one of: {string.Join(", ", Tasks)}";
        }

        if (manifest.Artifact is not { File: { Length: > 0 }, Sha256.Length: 64, Bytes: > 0, Format: "onnx" })
        {
            return "artifact requires file, 64-hex sha256, positive bytes, and format 'onnx'";
        }

        if (manifest.Inputs is not { Count: > 0 } || manifest.Outputs is not { Count: > 0 })
        {
            return "inputs and outputs must each list at least one tensor";
        }

        if (manifest.Preprocessing?.Id is null || manifest.Postprocessing?.Id is null)
        {
            return "preprocessing.id and postprocessing.id are required";
        }

        if (manifest.Compatibility?.RuntimeApi is null
            || !manifest.Compatibility.RuntimeApi.StartsWith(
                FileModelRegistry.SupportedRuntimeApi, StringComparison.Ordinal))
        {
            return $"compatibility.runtimeApi must target '{FileModelRegistry.SupportedRuntimeApi}'";
        }

        if (manifest.Compatibility.AllowedProviders is { Count: > 0 } providers
            && !providers.Contains("cpu", StringComparer.OrdinalIgnoreCase))
        {
            return "allowedProviders must include 'cpu' (the v1 baseline provider, ADR-0004)";
        }

        if (string.IsNullOrWhiteSpace(manifest.License) || string.IsNullOrWhiteSpace(manifest.Provenance))
        {
            return "license and provenance are required";
        }

        return null;
    }
}
