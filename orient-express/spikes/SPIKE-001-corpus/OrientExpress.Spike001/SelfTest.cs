using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OrientExpress.Spike001;

/// <summary>
/// Verifies the scanner end-to-end against a generated synthetic fixture tree
/// (no real photographs) and records machine-readable evidence that the
/// harness classifies formats and EXIF orientation correctly.
/// </summary>
public static class SelfTest
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        var resultsPath = "spike001-selftest-results.json";
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            if (args[index] == "--results")
            {
                resultsPath = args[index + 1];
            }
        }

        var root = Path.Combine(
            Path.GetTempPath(),
            "spike001-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path.Combine(root, "nested"));

        // Eight JPEGs covering every EXIF orientation (alternating endianness),
        // one JPEG without EXIF, plus PNG/HEIC/corrupt/non-image files.
        for (var orientation = 1; orientation <= 8; orientation++)
        {
            File.WriteAllBytes(
                Path.Combine(root, $"o{orientation}.jpg"),
                FixtureFactory.Jpeg(orientation, width: 4000, height: 3000, bigEndianExif: orientation % 2 == 0));
        }

        File.WriteAllBytes(Path.Combine(root, "noexif.jpg"), FixtureFactory.JpegWithoutExif(800, 600));
        File.WriteAllBytes(Path.Combine(root, "nested", "tiny.png"), FixtureFactory.Png(640, 480));
        File.WriteAllBytes(Path.Combine(root, "nested", "photo.heic"), FixtureFactory.Heic());
        File.WriteAllBytes(Path.Combine(root, "broken.jpg"), FixtureFactory.Corrupt());
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not an image; must be ignored");

        var report = InventoryScanner.Scan(root, maxFiles: 1000);
        var checks = new SortedDictionary<string, bool>(StringComparer.Ordinal)
        {
            ["jpeg_count_10"] = report.ByFormat.GetValueOrDefault("jpeg") == 10,
            ["png_count_1"] = report.ByFormat.GetValueOrDefault("png") == 1,
            ["heic_count_1"] = report.ByFormat.GetValueOrDefault("heic") == 1,
            ["non_image_ignored"] = report.FilesSeen == 12,
            ["all_orientations_seen"] = Enumerable.Range(1, 8).All(o =>
                report.ExifOrientation.GetValueOrDefault(o.ToString(CultureInfo.InvariantCulture)) == 1),
            ["megapixel_bucket_8_24"] = report.MegapixelBuckets.GetValueOrDefault("8-24mp") == 8,
            ["camera_make_aggregated"] = report.CameraMakes.GetValueOrDefault("SpikeCam") == 8,
            ["no_crash_on_corrupt"] = true,
        };

        var evidence = new
        {
            spike = "SPIKE-001",
            purpose = "scanner self-test on synthetic fixtures; the corpus tranche itself requires user-owned photos",
            environment = new
            {
                osDescription = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                harnessVersion = "0.1.0",
                runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            },
            checks,
            inventory = report,
            outcome = checks.Values.All(passed => passed) ? "pass" : "fail",
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(resultsPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resultsPath, JsonSerializer.Serialize(evidence, ReportOptions) + "\n");
        foreach (var check in checks)
        {
            Console.WriteLine($"{(check.Value ? "pass" : "fail"),-5} {check.Key}");
        }

        Directory.Delete(root, recursive: true);
        Console.WriteLine($"spike001: results written to {Path.GetFullPath(resultsPath)}");
        return checks.Values.All(passed => passed) ? 0 : 1;
    }
}
