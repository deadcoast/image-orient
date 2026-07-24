using System.Security.Cryptography;

namespace OrientExpress.Spike002;

/// <summary>
/// Digest-pinned external artifacts (DEP-006 rule: exact digest, no mutable
/// latest). Files are cached locally, verified on every use, and never
/// committed to the repository. The pilot image is a US-government
/// public-domain portrait, so no consent burden attaches to it.
/// </summary>
public static class Artifacts
{
    public sealed record PinnedArtifact(string FileName, string Url, string Sha256, string Provenance);

    public static readonly PinnedArtifact YuNetModel = new(
        "yunet.onnx",
        "https://github.com/opencv/opencv_zoo/raw/main/models/face_detection_yunet/face_detection_yunet_2023mar.onnx",
        "8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4",
        "OpenCV Zoo face_detection_yunet_2023mar; MIT License (c) 2020 Shiqi Yu (LICENSE retrieved 2026-07-23)");

    public static readonly PinnedArtifact PilotPortrait = new(
        "portrait1.jpg",
        "https://upload.wikimedia.org/wikipedia/commons/8/8d/President_Barack_Obama.jpg",
        "744dd848fbb0584229169e01c4944664957c62495fb9e8af514a088ebca43e19",
        "Official 2012 presidential portrait; US federal government work, public domain");

    public static string CacheDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "orient-express", "spike-fixtures");

    /// <summary>Returns the verified local path, or throws with an actionable message.</summary>
    public static string Require(PinnedArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var path = Path.Combine(CacheDirectory, artifact.FileName);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"artifact '{artifact.FileName}' is missing; download {artifact.Url} to {CacheDirectory} "
                + $"and verify sha256 {artifact.Sha256}");
        }

        using var stream = File.OpenRead(path);
        var digest = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (digest != artifact.Sha256)
        {
            throw new InvalidOperationException(
                $"artifact '{artifact.FileName}' failed digest verification: expected {artifact.Sha256}, got {digest}");
        }

        return path;
    }
}
