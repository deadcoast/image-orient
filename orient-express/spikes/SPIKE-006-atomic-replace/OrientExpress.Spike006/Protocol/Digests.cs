using System.Security.Cryptography;

namespace OrientExpress.Spike006.Protocol;

public static class Digests
{
    public static string Sha256Hex(byte[] content)
        => Convert.ToHexStringLower(SHA256.HashData(content));

    public static string Sha256HexOfFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
