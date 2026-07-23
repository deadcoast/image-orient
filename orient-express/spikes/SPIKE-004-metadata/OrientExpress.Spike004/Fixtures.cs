using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;

namespace OrientExpress.Spike004;

/// <summary>
/// Synthetic baseline-format fixtures: a real JPEG produced by cjpeg
/// (DEP-008 tools, digest-recorded) and a minimal valid RGB PNG written in
/// managed code. No user photographs are involved.
/// </summary>
public static class Fixtures
{
    public static string CreateJpeg(string cjpegPath, string directory, int width = 64, int height = 64)
    {
        var ppmPath = Path.Combine(directory, "fixture.ppm");
        var jpegPath = Path.Combine(directory, "fixture.jpg");
        using (var stream = File.Create(ppmPath))
        {
            var header = System.Text.Encoding.ASCII.GetBytes(
                string.Create(System.Globalization.CultureInfo.InvariantCulture, $"P6\n{width} {height}\n255\n"));
            stream.Write(header);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    stream.Write([(byte)(x * 4), (byte)(y * 4), (byte)((x + y) * 2)]);
                }
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = cjpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-quality", "90", "-outfile", jpegPath, ppmPath })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("failed to start cjpeg");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("cjpeg failed: " + process.StandardError.ReadToEnd());
        }

        File.Delete(ppmPath);
        return jpegPath;
    }

    /// <summary>Minimal valid 8×8 RGB PNG (IHDR + zlib IDAT + IEND with real CRCs).</summary>
    public static string CreatePng(string directory)
    {
        var path = Path.Combine(directory, "fixture.png");
        using var stream = File.Create(path);
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, 8);
        BinaryPrimitives.WriteUInt32BigEndian(ihdr[4..], 8);
        ihdr[8] = 8;
        ihdr[9] = 2;
        WriteChunk(stream, "IHDR", ihdr.ToArray());

        var raw = new byte[8 * ((8 * 3) + 1)];
        for (var y = 0; y < 8; y++)
        {
            var row = y * 25;
            raw[row] = 0;
            for (var x = 0; x < 8; x++)
            {
                raw[row + 1 + (x * 3)] = (byte)(x * 32);
                raw[row + 2 + (x * 3)] = (byte)(y * 32);
                raw[row + 3 + (x * 3)] = 128;
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        WriteChunk(stream, "IDAT", compressed.ToArray());
        WriteChunk(stream, "IEND", []);
        return path;
    }

    public static bool LooksLikeValidPng(string path)
    {
        var data = File.ReadAllBytes(path);
        return data.Length > 20
            && data.AsSpan(0, 8).SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
            && data.AsSpan()[^8..].IndexOf("IEND"u8) >= 0;
    }

    /// <summary>JPEG marker inventory for structural before/after comparison.</summary>
    public static IReadOnlyDictionary<string, int> JpegSegmentInventory(string path)
    {
        var data = File.ReadAllBytes(path);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var offset = 2;
        while (offset + 4 <= data.Length && data[offset] == 0xFF)
        {
            var marker = data[offset + 1];
            var name = marker switch
            {
                >= 0xE0 and <= 0xEF => "APP" + (marker - 0xE0).ToString(System.Globalization.CultureInfo.InvariantCulture),
                0xDB => "DQT",
                0xC4 => "DHT",
                >= 0xC0 and <= 0xC2 => "SOF",
                0xDA => "SOS",
                0xFE => "COM",
                _ => "M" + marker.ToString("X2", System.Globalization.CultureInfo.InvariantCulture),
            };
            counts[name] = counts.GetValueOrDefault(name) + 1;
            if (marker == 0xDA)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset + 2));
            offset += 2 + length;
        }

        return counts;
    }

    private static void WriteChunk(Stream stream, string type, byte[] payload)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)payload.Length);
        stream.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(payload);
        Span<byte> crc = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeBytes, payload));
        stream.Write(crc);
    }

    private static uint Crc32(byte[] type, byte[] payload)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var chunk in new[] { type, payload })
        {
            foreach (var value in chunk)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(crc & 1));
                }
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
