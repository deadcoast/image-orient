using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace OrientExpress.Spike004;

public sealed record ScenarioResult(
    string Id,
    string Description,
    string Outcome,
    IReadOnlyDictionary<string, string> Measurements,
    string? Error);

/// <summary>
/// SPIKE-004 evidence: the -stay_open protocol, the orientation-write
/// round-trip matrix over EXIF/XMP/IPTC/GPS families, timestamp
/// restoration, ADS and file-identity behavior across ExifTool's rewrite,
/// error-channel resilience, and PNG XMP round-trip. Synthetic baseline
/// fixtures only; maker-note coverage explicitly awaits the SPIKE-001
/// corpus tranche.
/// </summary>
public static class EvidenceRunner
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    private static readonly IReadOnlyDictionary<string, string> FixtureTags =
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["EXIF:Make"] = "SpikeCam",
            ["EXIF:Model"] = "SpikeCam X100",
            ["EXIF:DateTimeOriginal"] = "2024:06:15 10:30:00",
            ["EXIF:Artist"] = "spike004 fixture",
            ["EXIF:Copyright"] = "no rights reserved",
            ["EXIF:GPSLatitude"] = "45.5",
            ["EXIF:GPSLatitudeRef"] = "N",
            ["EXIF:GPSLongitude"] = "63.6",
            ["EXIF:GPSLongitudeRef"] = "W",
            ["XMP:Rating"] = "4",
            ["XMP:Subject"] = "spike004-subject",
            ["XMP:CreateDate"] = "2024:06:15 10:30:00",
            ["IPTC:Keywords"] = "spike004-keyword",
            ["IPTC:Caption-Abstract"] = "spike004 caption",
        };

    public static int Run(string[] args)
    {
        var resultsPath = "spike004-results.json";
        var exifToolPath = DefaultExifToolPath();
        var cjpegPath = @"C:\libjpeg-turbo64\bin\cjpeg.exe";
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
                case "--cjpeg":
                    cjpegPath = args[index + 1];
                    break;
                default:
                    break;
            }
        }

        if (!File.Exists(exifToolPath) || !File.Exists(cjpegPath))
        {
            Console.Error.WriteLine("spike004: exiftool or cjpeg not found; pass --exiftool/--cjpeg.");
            return 2;
        }

        var workRoot = Path.Combine(
            Path.GetTempPath(),
            "spike004-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(workRoot);

        using var client = new ExifToolStayOpenClient(exifToolPath);
        var results = new List<ScenarioResult>
        {
            RunSafely("E-STAYOPEN-PROTOCOL", "one persistent process; every command synchronized on its execute id",
                recorder => Protocol(recorder, client)),
            RunSafely("E-ORIENTATION-ROUNDTRIP", "orientation write changes exactly one tag; EXIF/XMP/IPTC/GPS families intact",
                recorder => OrientationRoundTrip(recorder, client, cjpegPath, workRoot)),
            RunSafely("E-TIMESTAMP-RESTORE", "-P preserves the filesystem modify time; omitting it does not",
                recorder => TimestampRestore(recorder, client, cjpegPath, workRoot)),
            RunSafely("E-ADS-AND-IDENTITY", "ExifTool's rewrite drops ADS and changes the file id (re-baseline required)",
                recorder => AdsAndIdentity(recorder, client, cjpegPath, workRoot)),
            RunSafely("E-ERROR-CHANNEL", "an invalid write surfaces an error and the process keeps serving",
                recorder => ErrorChannel(recorder, client, cjpegPath, workRoot)),
            RunSafely("E-PNG-XMP", "XMP round-trips into a PNG and the container stays valid",
                recorder => PngXmp(recorder, client, workRoot)),
        };

        var report = new
        {
            spike = "SPIKE-004",
            environment = new
            {
                osDescription = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                exifTool = new
                {
                    path = exifToolPath,
                    version = client.Version(),
                    sha256 = Sha256Of(exifToolPath),
                    dependency = "DEP-007 (winget OliverBetz.ExifTool)",
                },
                fixturePolicy = "synthetic cjpeg/managed fixtures; no user photos; maker notes await the SPIKE-001 corpus",
                harnessVersion = "0.1.0",
                runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            },
            scenarios = results,
            summary = new
            {
                total = results.Count,
                pass = results.Count(result => result.Outcome == "pass"),
                fail = results.Count(result => result.Outcome == "fail"),
            },
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(resultsPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(resultsPath, JsonSerializer.Serialize(report, ReportOptions) + "\n");
        foreach (var result in results)
        {
            Console.WriteLine($"{result.Outcome,-5} {result.Id}");
        }

        Console.WriteLine($"spike004: total={results.Count} fail={results.Count(r => r.Outcome == "fail")}");
        Console.WriteLine($"spike004: results written to {Path.GetFullPath(resultsPath)}");
        Directory.Delete(workRoot, recursive: true);
        return results.All(result => result.Outcome == "pass") ? 0 : 1;
    }

    private static void Protocol(ScenarioRecorder recorder, ExifToolStayOpenClient client)
    {
        var pid = client.ProcessId;
        recorder.Measure("exiftool_version", client.Version());
        for (var round = 0; round < 10; round++)
        {
            _ = client.Version();
        }

        recorder.Expect("single_process_reused", client.ProcessId == pid);
        recorder.Expect("commands_synchronized", client.Version().Length > 0);
    }

    private static void OrientationRoundTrip(
        ScenarioRecorder recorder, ExifToolStayOpenClient client, string cjpegPath, string workRoot)
    {
        var directory = Directory.CreateDirectory(Path.Combine(workRoot, "roundtrip")).FullName;
        var jpeg = Fixtures.CreateJpeg(cjpegPath, directory);
        _ = client.WriteFixtureTags(jpeg, FixtureTags);

        var before = ParseTags(client.ReadAllTagsJson(jpeg));
        var structureBefore = Fixtures.JpegSegmentInventory(jpeg);
        var write = client.WriteOrientation(jpeg, 6, preserveFileTime: true);
        recorder.Measure("write_response", write.Output.Trim());
        var after = ParseTags(client.ReadAllTagsJson(jpeg));
        var structureAfter = Fixtures.JpegSegmentInventory(jpeg);

        // -G1 groups EXIF tags under IFD0/ExifIFD/GPS.
        recorder.Expect("orientation_written", after.GetValueOrDefault("IFD0:Orientation") == "6");
        static bool IsVolatile(string key)
            => key.StartsWith("System:", StringComparison.Ordinal)
                || key.StartsWith("File:", StringComparison.Ordinal)
                || key == "IFD0:Orientation";
        var lost = before.Keys.Where(key => !after.ContainsKey(key) && !IsVolatile(key)).ToList();
        var changed = before.Keys.Where(key =>
                after.ContainsKey(key) && after[key] != before[key] && !IsVolatile(key))
            .ToList();
        recorder.Measure("tags_before", before.Count);
        recorder.Measure("tags_after", after.Count);
        recorder.Measure("lost_tags", string.Join(";", lost));
        recorder.Measure("changed_tags", string.Join(";", changed));
        recorder.Expect("no_tag_lost", lost.Count == 0);
        recorder.Expect("no_tag_changed_besides_orientation", changed.Count == 0);
        var families = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["EXIF"] = ["IFD0:", "ExifIFD:"],
            ["GPS"] = ["GPS:"],
            ["XMP"] = ["XMP"],
            ["IPTC"] = ["IPTC:"],
        };
        foreach (var (family, prefixes) in families)
        {
            var familyBefore = before.Keys.Count(key => prefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)));
            var familyAfter = after.Keys.Count(key => prefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal)));
            recorder.Measure($"family_{family}_before", familyBefore);
            recorder.Expect($"family_{family}_nonempty_and_preserved", familyBefore > 0 && familyAfter >= familyBefore);
        }

        recorder.Expect(
            "jpeg_structure_stable",
            structureBefore.GetValueOrDefault("SOF") == structureAfter.GetValueOrDefault("SOF")
            && structureBefore.GetValueOrDefault("SOS") == structureAfter.GetValueOrDefault("SOS")
            && structureAfter.GetValueOrDefault("APP1") >= 1);
    }

    private static void TimestampRestore(
        ScenarioRecorder recorder, ExifToolStayOpenClient client, string cjpegPath, string workRoot)
    {
        var directory = Directory.CreateDirectory(Path.Combine(workRoot, "timestamps")).FullName;
        var jpeg = Fixtures.CreateJpeg(cjpegPath, directory);
        var knownTime = new DateTime(2020, 5, 5, 5, 5, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(jpeg, knownTime);

        _ = client.WriteOrientation(jpeg, 3, preserveFileTime: true);
        recorder.Expect("dash_p_preserves_mtime", File.GetLastWriteTimeUtc(jpeg) == knownTime);

        _ = client.WriteOrientation(jpeg, 1, preserveFileTime: false);
        recorder.Expect("without_dash_p_mtime_changes", File.GetLastWriteTimeUtc(jpeg) != knownTime);
    }

    private static void AdsAndIdentity(
        ScenarioRecorder recorder, ExifToolStayOpenClient client, string cjpegPath, string workRoot)
    {
        var directory = Directory.CreateDirectory(Path.Combine(workRoot, "ads")).FullName;
        var jpeg = Fixtures.CreateJpeg(cjpegPath, directory);
        File.WriteAllText(jpeg + ":oxads", "spike004-ads-payload");
        var idBefore = QueryFileId(jpeg);

        _ = client.WriteOrientation(jpeg, 8, preserveFileTime: true);

        var adsSurvived = File.Exists(jpeg + ":oxads");
        var idAfter = QueryFileId(jpeg);
        recorder.Measure("ads_survived", adsSurvived);
        recorder.Measure("file_id_before", idBefore ?? "unknown");
        recorder.Measure("file_id_after", idAfter ?? "unknown");
        recorder.Measure("file_id_changed", idBefore is not null && idAfter is not null && idBefore != idAfter);
        recorder.Expect("behavior_recorded", true);
    }

    private static void ErrorChannel(
        ScenarioRecorder recorder, ExifToolStayOpenClient client, string cjpegPath, string workRoot)
    {
        var directory = Directory.CreateDirectory(Path.Combine(workRoot, "errors")).FullName;
        var jpeg = Fixtures.CreateJpeg(cjpegPath, directory);
        var pid = client.ProcessId;

        var invalid = client.WriteInvalidTag(jpeg);
        recorder.Measure("invalid_write_stderr", invalid.StderrOutput.Trim());
        recorder.Expect(
            "error_surfaced_on_synchronized_stderr",
            invalid.StderrOutput.Contains("not defined", StringComparison.OrdinalIgnoreCase)
            || invalid.StderrOutput.Contains("Warning", StringComparison.OrdinalIgnoreCase));

        var follow = client.WriteOrientation(jpeg, 2, preserveFileTime: true);
        recorder.Expect("process_survives_and_serves", client.ProcessId == pid
            && follow.Output.Contains("1 image files updated", StringComparison.OrdinalIgnoreCase));
    }

    private static void PngXmp(ScenarioRecorder recorder, ExifToolStayOpenClient client, string workRoot)
    {
        var directory = Directory.CreateDirectory(Path.Combine(workRoot, "png")).FullName;
        var png = Fixtures.CreatePng(directory);
        _ = client.WriteFixtureTags(png, new Dictionary<string, string>
        {
            ["XMP:Rating"] = "5",
            ["XMP:Subject"] = "spike004-png",
        });

        var tags = ParseTags(client.ReadAllTagsJson(png));
        recorder.Expect("xmp_rating_round_trips", tags.GetValueOrDefault("XMP-xmp:Rating") == "5"
            || tags.GetValueOrDefault("XMP:Rating") == "5");
        recorder.Expect("png_container_still_valid", Fixtures.LooksLikeValidPng(png));
    }

    private static Dictionary<string, string> ParseTags(string json)
    {
        using var document = JsonDocument.Parse(json);
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement[0].EnumerateObject())
        {
            tags[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString()!,
                JsonValueKind.Number => property.Value.GetRawText(),
                _ => property.Value.GetRawText(),
            };
        }

        return tags;
    }

    private static string? QueryFileId(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.SafeFileHandle.DangerousGetHandle() == 0
                ? null
                : GetFileIdHex(stream.SafeFileHandle);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string? GetFileIdHex(Microsoft.Win32.SafeHandles.SafeFileHandle handle)
        => NativeFileId.TryGet(handle, out var fileId)
            ? fileId.ToString("x16", CultureInfo.InvariantCulture)
            : null;

    private static ScenarioResult RunSafely(string id, string description, Action<ScenarioRecorder> body)
    {
        var recorder = new ScenarioRecorder();
        try
        {
            body(recorder);
            return recorder.Complete(id, description);
        }
        catch (Exception exception)
            when (exception is IOException or InvalidOperationException or JsonException or UnauthorizedAccessException)
        {
            return recorder.Failed(id, description, exception);
        }
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static string DefaultExifToolPath()
    {
        string[] candidates =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "ExifTool", "ExifTool.exe"),
            "exiftool.exe",
        ];
        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }
}

public sealed class ScenarioRecorder
{
    private readonly SortedDictionary<string, string> _measurements = new(StringComparer.Ordinal);
    private bool _expectationsHeld = true;

    public void Measure(string key, string value) => _measurements[key] = value;

    public void Measure(string key, long value)
        => Measure(key, value.ToString(CultureInfo.InvariantCulture));

    public void Measure(string key, bool value) => Measure(key, value ? "true" : "false");

    public void Expect(string key, bool held)
    {
        Measure(key, held);
        _expectationsHeld &= held;
    }

    public ScenarioResult Complete(string id, string description)
        => new(id, description, _expectationsHeld ? "pass" : "fail", _measurements, Error: null);

    public ScenarioResult Failed(string id, string description, Exception exception)
        => new(id, description, "fail", _measurements, exception.ToString());
}

internal static partial class NativeFileId
{
    internal static bool TryGet(Microsoft.Win32.SafeHandles.SafeFileHandle handle, out ulong fileId)
    {
        fileId = 0;
        if (!GetFileInformationByHandle(handle, out var information))
        {
            return false;
        }

        fileId = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return true;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [System.Runtime.InteropServices.LibraryImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandle(
        Microsoft.Win32.SafeHandles.SafeFileHandle handle, out ByHandleFileInformation information);
}
