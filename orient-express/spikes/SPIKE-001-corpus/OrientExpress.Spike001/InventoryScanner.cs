using System.Globalization;
using System.Text.Json;

namespace OrientExpress.Spike001;

public sealed record InventoryReport(
    string SchemaVersion,
    string RunUtc,
    long FilesSeen,
    long FilesProbed,
    long Unreadable,
    IReadOnlyDictionary<string, long> ByFormat,
    IReadOnlyDictionary<string, long> ExifOrientation,
    IReadOnlyDictionary<string, long> MegapixelBuckets,
    IReadOnlyDictionary<string, long> SizeBuckets,
    IReadOnlyDictionary<string, long> CameraMakes);

/// <summary>
/// Opt-in, read-only corpus inventory (SPIKE-001). Emits aggregates only:
/// no paths, no file names, no thumbnails, nothing per-file. Camera make is
/// the only string taken from file content, truncated to a small allow-set
/// size and aggregated as counts.
/// </summary>
public static class InventoryScanner
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".png", ".heic", ".heif", ".avif", ".tif", ".tiff", ".gif", ".bmp", ".webp",
    };

    public static InventoryReport Scan(string root, int maxFiles)
    {
        long seen = 0, probed = 0, unreadable = 0;
        var byFormat = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var orientations = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var megapixels = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var sizes = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var makes = new SortedDictionary<string, long>(StringComparer.Ordinal);
        var buffer = new byte[FormatSniffer.ProbeBytes];

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System,
        };
        foreach (var path in Directory.EnumerateFiles(root, "*", options))
        {
            if (!ImageExtensions.Contains(Path.GetExtension(path)))
            {
                continue;
            }

            if (++seen > maxFiles)
            {
                seen--;
                break;
            }

            long length;
            int read;
            try
            {
                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                length = stream.Length;
                read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            }
            catch (IOException)
            {
                unreadable++;
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                unreadable++;
                continue;
            }

            probed++;
            var probe = FormatSniffer.Probe(buffer.AsSpan(0, read));
            Bump(byFormat, probe.Format);
            if (probe.Format == "jpeg")
            {
                Bump(orientations, probe.ExifOrientation?.ToString(CultureInfo.InvariantCulture) ?? "absent");
            }

            Bump(megapixels, MegapixelBucket(probe.Width, probe.Height));
            Bump(sizes, SizeBucket(length));
            if (!string.IsNullOrEmpty(probe.CameraMake))
            {
                Bump(makes, probe.CameraMake);
            }
        }

        return new InventoryReport(
            SchemaVersion: "1.0",
            RunUtc: DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            FilesSeen: seen,
            FilesProbed: probed,
            Unreadable: unreadable,
            ByFormat: byFormat,
            ExifOrientation: orientations,
            MegapixelBuckets: megapixels,
            SizeBuckets: sizes,
            CameraMakes: makes);
    }

    public static void Write(InventoryReport report, string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(report, ReportOptions) + "\n");
    }

    private static void Bump(SortedDictionary<string, long> counts, string key)
        => counts[key] = counts.GetValueOrDefault(key) + 1;

    private static string MegapixelBucket(int? width, int? height)
    {
        if (width is null || height is null)
        {
            return "unknown";
        }

        var megapixels = (double)width.Value * height.Value / 1_000_000.0;
        return megapixels switch
        {
            < 0.3 => "lt-0.3mp",
            < 2 => "0.3-2mp",
            < 8 => "2-8mp",
            < 24 => "8-24mp",
            _ => "gte-24mp",
        };
    }

    private static string SizeBucket(long bytes)
        => bytes switch
        {
            < 100_000 => "lt-100kb",
            < 1_000_000 => "100kb-1mb",
            < 5_000_000 => "1-5mb",
            < 20_000_000 => "5-20mb",
            _ => "gte-20mb",
        };
}
