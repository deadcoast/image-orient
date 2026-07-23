using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OrientExpress.Spike006.Protocol;

public sealed record JobManifest(
    string JobId,
    string SourcePath,
    string BackupPath,
    string JournalPath,
    string QuarantineDirectory,
    string OriginalSha256,
    string ExpectedSha256)
{
    public string SourceDirectory => Path.GetDirectoryName(SourcePath)
        ?? throw new InvalidOperationException($"Source path '{SourcePath}' has no directory.");
}

/// <summary>
/// Builds deterministic job fixtures: content derives from the job id alone so
/// a run is repeatable from a clean checkout with no fixture assets.
/// </summary>
public static class JobFixture
{
    public const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static JobManifest Create(string jobDirectory, string jobId, int contentLength = 64 * 1024)
    {
        Directory.CreateDirectory(jobDirectory);
        var sourcePath = Path.Combine(jobDirectory, "source.bin");
        var content = DeterministicBytes(jobId, contentLength);
        File.WriteAllBytes(sourcePath, content);

        var manifest = new JobManifest(
            JobId: jobId,
            SourcePath: sourcePath,
            BackupPath: Path.Combine(jobDirectory, "backup.bin"),
            JournalPath: Path.Combine(jobDirectory, "journal.jsonl"),
            QuarantineDirectory: Path.Combine(jobDirectory, "quarantine"),
            OriginalSha256: Digests.Sha256Hex(content),
            ExpectedSha256: Digests.Sha256Hex(ContentTransform.Apply(content)));

        File.WriteAllText(
            Path.Combine(jobDirectory, ManifestFileName),
            JsonSerializer.Serialize(manifest, SerializerOptions));
        return manifest;
    }

    public static JobManifest Load(string jobDirectory)
    {
        var manifestPath = Path.Combine(jobDirectory, ManifestFileName);
        return JsonSerializer.Deserialize<JobManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidOperationException($"Manifest at '{manifestPath}' is empty.");
    }

    /// <summary>Seeded xorshift so fixtures need no random source and no committed binary assets.</summary>
    public static byte[] DeterministicBytes(string seedText, int length)
    {
        var seed = BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes(seedText)), 0);
        var state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        var content = new byte[length];
        for (var index = 0; index < content.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            content[index] = (byte)state;
        }

        return content;
    }
}
