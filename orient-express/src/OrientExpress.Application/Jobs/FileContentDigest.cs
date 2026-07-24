using System.Security.Cryptography;

namespace OrientExpress.Application.Jobs;

/// <summary>Full-content SHA-256, the DOC-DATAMODEL strong fingerprint used before and after destructive apply.</summary>
public static class FileContentDigest
{
    public static string Sha256OfFile(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
