using OrientExpress.Contracts.Config;

namespace OrientExpress.Application.Config;

/// <summary>
/// Deterministic layer merge (AC-036): for every leaf the highest-precedence
/// layer that supplies a value wins, and its layer name is recorded as that
/// leaf's provenance. Lists replace wholesale; they never concatenate.
/// </summary>
public static class ConfigMerger
{
    /// <param name="layersHighestFirst">Ordered CLI, environment, explicit file, user file, defaults.</param>
    public static (UprightConfig Merged, IReadOnlyDictionary<string, string> Provenance) Merge(
        IReadOnlyList<ConfigLayer> layersHighestFirst)
    {
        ArgumentNullException.ThrowIfNull(layersHighestFirst);
        var provenance = new SortedDictionary<string, string>(StringComparer.Ordinal);

        var merged = new UprightConfig
        {
            SchemaVersion = Pick(layersHighestFirst, provenance, "schemaVersion", config => config.SchemaVersion),
            Paths = new PathsConfig
            {
                JobRoot = Pick(layersHighestFirst, provenance, "paths.jobRoot", config => config.Paths?.JobRoot),
                ModelRoot = Pick(layersHighestFirst, provenance, "paths.modelRoot", config => config.Paths?.ModelRoot),
                CacheRoot = Pick(layersHighestFirst, provenance, "paths.cacheRoot", config => config.Paths?.CacheRoot),
                BackupRoot = Pick(layersHighestFirst, provenance, "paths.backupRoot", config => config.Paths?.BackupRoot),
            },
            Discovery = new DiscoveryConfig
            {
                Recurse = PickValue<bool>(layersHighestFirst, provenance, "discovery.recurse", config => config.Discovery?.Recurse),
                FollowReparsePoints = PickValue<bool>(layersHighestFirst, provenance, "discovery.followReparsePoints", config => config.Discovery?.FollowReparsePoints),
                Include = Pick(layersHighestFirst, provenance, "discovery.include", config => config.Discovery?.Include),
                Exclude = Pick(layersHighestFirst, provenance, "discovery.exclude", config => config.Discovery?.Exclude),
                MaxFiles = PickValue<long>(layersHighestFirst, provenance, "discovery.maxFiles", config => config.Discovery?.MaxFiles),
                MaxBytes = PickValue<long>(layersHighestFirst, provenance, "discovery.maxBytes", config => config.Discovery?.MaxBytes),
            },
            Analysis = new AnalysisConfig
            {
                Profile = Pick(layersHighestFirst, provenance, "analysis.profile", config => config.Analysis?.Profile),
                MaxThumbnailLongEdge = PickValue<int>(layersHighestFirst, provenance, "analysis.maxThumbnailLongEdge", config => config.Analysis?.MaxThumbnailLongEdge),
                FaceModel = Pick(layersHighestFirst, provenance, "analysis.faceModel", config => config.Analysis?.FaceModel),
                SceneFallback = PickValue<bool>(layersHighestFirst, provenance, "analysis.sceneFallback", config => config.Analysis?.SceneFallback),
                SequenceConsensus = PickValue<bool>(layersHighestFirst, provenance, "analysis.sequenceConsensus", config => config.Analysis?.SequenceConsensus),
            },
            Output = new OutputConfig
            {
                Mode = Pick(layersHighestFirst, provenance, "output.mode", config => config.Output?.Mode),
                Collision = Pick(layersHighestFirst, provenance, "output.collision", config => config.Output?.Collision),
                JpegEdge = Pick(layersHighestFirst, provenance, "output.jpegEdge", config => config.Output?.JpegEdge),
                MetadataProfile = Pick(layersHighestFirst, provenance, "output.metadataProfile", config => config.Output?.MetadataProfile),
                PreserveFilesystemTimes = PickValue<bool>(layersHighestFirst, provenance, "output.preserveFilesystemTimes", config => config.Output?.PreserveFilesystemTimes),
            },
            Resources = new ResourcesConfig
            {
                MemoryMiB = PickValue<int>(layersHighestFirst, provenance, "resources.memoryMiB", config => config.Resources?.MemoryMiB),
                MetadataWorkers = PickValue<int>(layersHighestFirst, provenance, "resources.metadataWorkers", config => config.Resources?.MetadataWorkers),
                DecodeWorkers = PickValue<int>(layersHighestFirst, provenance, "resources.decodeWorkers", config => config.Resources?.DecodeWorkers),
                InferenceWorkers = PickValue<int>(layersHighestFirst, provenance, "resources.inferenceWorkers", config => config.Resources?.InferenceWorkers),
                TransformWorkers = PickValue<int>(layersHighestFirst, provenance, "resources.transformWorkers", config => config.Resources?.TransformWorkers),
                VerificationWorkers = PickValue<int>(layersHighestFirst, provenance, "resources.verificationWorkers", config => config.Resources?.VerificationWorkers),
                QueueCapacity = PickValue<int>(layersHighestFirst, provenance, "resources.queueCapacity", config => config.Resources?.QueueCapacity),
            },
            Privacy = new PrivacyConfig
            {
                Telemetry = PickValue<bool>(layersHighestFirst, provenance, "privacy.telemetry", config => config.Privacy?.Telemetry),
                ReviewThumbnails = PickValue<bool>(layersHighestFirst, provenance, "privacy.reviewThumbnails", config => config.Privacy?.ReviewThumbnails),
                RedactPathsInDiagnostics = PickValue<bool>(layersHighestFirst, provenance, "privacy.redactPathsInDiagnostics", config => config.Privacy?.RedactPathsInDiagnostics),
                DiagnosticRetentionDays = PickValue<int>(layersHighestFirst, provenance, "privacy.diagnosticRetentionDays", config => config.Privacy?.DiagnosticRetentionDays),
            },
        };
        return (merged, provenance);
    }

    private static TValue? Pick<TValue>(
        IReadOnlyList<ConfigLayer> layers,
        SortedDictionary<string, string> provenance,
        string path,
        Func<UprightConfig, TValue?> selector)
        where TValue : class
    {
        foreach (var layer in layers)
        {
            if (layer.Values is not null && selector(layer.Values) is { } value)
            {
                provenance[path] = layer.Name;
                return value;
            }
        }

        return null;
    }

    private static TValue? PickValue<TValue>(
        IReadOnlyList<ConfigLayer> layers,
        SortedDictionary<string, string> provenance,
        string path,
        Func<UprightConfig, TValue?> selector)
        where TValue : struct
    {
        foreach (var layer in layers)
        {
            if (layer.Values is not null && selector(layer.Values) is { } value)
            {
                provenance[path] = layer.Name;
                return value;
            }
        }

        return null;
    }
}
