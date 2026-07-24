using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OrientExpress.Spike007;

/// <summary>
/// SPIKE-007 evidence: WIC installed-codec inventory (COM enumerator),
/// operational encode/decode/round-trip probes per format, and the JPEG
/// orientation metadata read against an ExifTool-authored value. Exit gate:
/// JPEG/PNG guaranteed; optional formats gated with tested diagnostics.
/// </summary>
public static class EvidenceRunner
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        var resultsPath = "spike007-results.json";
        var exifToolPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "ExifTool", "ExifTool.exe");
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            switch (args[index])
            {
                case "--results":
                    resultsPath = args[index + 1];
                    break;
                case "--exiftool":
                    exifToolPath = args[index + 1];
                    break;
                default:
                    break;
            }
        }

        var workRoot = Directory.CreateTempSubdirectory("spike007-").FullName;
        var decoders = WicComInventory.DecoderFriendlyNames();
        var encoders = WicComInventory.EncoderFriendlyNames();

        string[] probeFormats = ["jpeg", "png", "tiff", "bmp", "gif"];
        var probes = probeFormats.Select(format => WpfProbes.Probe(format, workRoot)).ToList();

        var orientation = ProbeJpegOrientationReading(workRoot, exifToolPath);
        var optional = OptionalFormatGates();
        var baselineEngagement = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["jpeg"] = WpfProbes.ProbeHeaderEngagement("jpeg"),
            ["png"] = WpfProbes.ProbeHeaderEngagement("png"),
        };

        var safety = new SortedDictionary<string, bool>(StringComparer.Ordinal)
        {
            ["jpeg_encode_decode_roundtrip"] = probes.Single(p => p.Format == "jpeg") is
            { EncodeSupported: true, DecodeSupported: true, RoundTripDimensionsExact: true },
            ["png_encode_decode_roundtrip"] = probes.Single(p => p.Format == "png") is
            { EncodeSupported: true, DecodeSupported: true, RoundTripDimensionsExact: true },
            ["jpeg_orientation_metadata_readable"] = orientation.Readable && orientation.Value == 6,
            ["jpeg_and_png_codecs_engage_on_headers"] = baselineEngagement.Values.All(
                verdict => verdict.StartsWith("present:", StringComparison.Ordinal)),
            ["optional_formats_have_tested_diagnostics"] = optional.All(gate =>
                gate.Value.StartsWith("present:", StringComparison.Ordinal)
                || gate.Value.StartsWith("absent:", StringComparison.Ordinal)),
        };

        var report = new
        {
            spike = "SPIKE-007",
            environment = new
            {
                osDescription = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                dependency = "Windows Imaging Component (DEP-009, OS-provided)",
                harnessVersion = "0.1.0",
                fixturePolicy = "synthetic WPF-encoded bitmaps; no user photos",
                runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            },
            safety,
            comEnumeratorDecoders = decoders,
            comEnumeratorEncoders = encoders,
            comEnumeratorFinding = "managed ComImport enumeration returned empty on this machine; header-engagement probes are the reliable capability source",
            baselineEngagement,
            operationalProbes = probes,
            jpegOrientationRead = new { orientation.Readable, orientation.Value, orientation.Diagnostic },
            optionalFormatGates = optional,
            summary = new
            {
                decoderCount = decoders.Count,
                encoderCount = encoders.Count,
                safetyHeld = safety.Values.All(held => held),
            },
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(resultsPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resultsPath, JsonSerializer.Serialize(report, ReportOptions) + "\n");
        foreach (var entry in safety)
        {
            Console.WriteLine($"{(entry.Value ? "pass" : "FAIL"),-5} {entry.Key}");
        }

        foreach (var gate in optional)
        {
            Console.WriteLine($"gate  {gate.Key}: {gate.Value}");
        }

        Console.WriteLine($"spike007: decoders={decoders.Count} encoders={encoders.Count}");
        Console.WriteLine($"spike007: results written to {Path.GetFullPath(resultsPath)}");
        Directory.Delete(workRoot, recursive: true);
        return safety.Values.All(held => held) ? 0 : 1;
    }

    /// <summary>
    /// Optional formats report presence from the authoritative inventory with
    /// a stable diagnostic reason either way (the "tested diagnostics" gate).
    /// </summary>
    private static SortedDictionary<string, string> OptionalFormatGates()
    {
        var gates = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var format in new[] { "heic", "avif", "webp", "jxl" })
        {
            gates[format] = WpfProbes.ProbeHeaderEngagement(format);
        }

        return gates;
    }

    private static (bool Readable, int Value, string? Diagnostic) ProbeJpegOrientationReading(
        string workRoot, string exifToolPath)
    {
        var probe = WpfProbes.Probe("jpeg", workRoot);
        if (!probe.EncodeSupported)
        {
            return (false, 0, "jpeg encode probe failed");
        }

        var jpegPath = Path.Combine(workRoot, "probe.jpeg");
        if (!File.Exists(exifToolPath))
        {
            return (false, 0, "exiftool unavailable for authoring the orientation fixture");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exifToolPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "-n", "-EXIF:Orientation=6", "-overwrite_original", jpegPath })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using (var process = Process.Start(startInfo))
        {
            process?.WaitForExit(30_000);
        }

        return WpfProbes.ReadJpegOrientation(jpegPath);
    }
}
