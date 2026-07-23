using OrientExpress.Contracts.Config;

namespace OrientExpress.Application.Config;

/// <summary>
/// Built-in defaults (lowest precedence layer), mirroring the `default`
/// annotations in <c>schemas/config.schema.json</c>. Root paths are supplied
/// by the composition root so this layer stays pure.
/// </summary>
public static class ConfigDefaults
{
    public static UprightConfig Create(string dataRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        return new UprightConfig
        {
            SchemaVersion = "1.0",
            Paths = new PathsConfig
            {
                JobRoot = System.IO.Path.Combine(dataRoot, "jobs"),
                ModelRoot = System.IO.Path.Combine(dataRoot, "models"),
                CacheRoot = System.IO.Path.Combine(dataRoot, "cache"),
                BackupRoot = null,
            },
            Discovery = new DiscoveryConfig
            {
                Recurse = true,
                FollowReparsePoints = false,
                Include = ["**/*.jpg", "**/*.jpeg", "**/*.png"],
                Exclude = [],
                MaxFiles = null,
                MaxBytes = null,
            },
            Analysis = new AnalysisConfig
            {
                Profile = "conservative",
                MaxThumbnailLongEdge = 1280,
                FaceModel = "yunet-baseline",
                SceneFallback = false,
                SequenceConsensus = false,
            },
            Output = new OutputConfig
            {
                Mode = "safe-copy",
                Collision = "error",
                JpegEdge = "abort",
                MetadataProfile = "preserve-personal",
                PreserveFilesystemTimes = true,
            },
            Resources = new ResourcesConfig
            {
                MemoryMiB = 1536,
                MetadataWorkers = 2,
                DecodeWorkers = 2,
                InferenceWorkers = 1,
                TransformWorkers = 2,
                VerificationWorkers = 2,
                QueueCapacity = 256,
            },
            Privacy = new PrivacyConfig
            {
                Telemetry = false,
                ReviewThumbnails = false,
                RedactPathsInDiagnostics = true,
                DiagnosticRetentionDays = 14,
            },
        };
    }
}
