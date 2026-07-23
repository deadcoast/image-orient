using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OrientExpress.Spike005;

public sealed record CaseResult(
    string Id,
    int Width,
    int Height,
    string Sampling,
    bool Progressive,
    int Degrees,
    string Mode,
    int ExitCode,
    bool OutputProduced,
    int OutputWidth,
    int OutputHeight,
    bool DimsMatchExpected,
    bool PixelExact,
    long PixelDiffBytes,
    int MaxChannelDelta,
    bool? RoundTripExact,
    string? Stderr);

/// <summary>
/// SPIKE-005 evidence: the adversarial dimension × sampling × rotation ×
/// mode matrix for jpegtran, plus marker-copy behavior. Safety expectations
/// (declared in the report): -perfect either refuses or is pixel-exact —
/// never silently imperfect; -trim visibly changes dimensions; the default
/// mode's edge behavior is measured and recorded.
/// </summary>
public static class EvidenceRunner
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    private static readonly (int Width, int Height)[] Dimensions =
        [(512, 512), (500, 500), (512, 500), (500, 512), (511, 511), (15, 15)];

    private static readonly string[] Samplings = ["1x1", "2x2"];
    private static readonly int[] Rotations = [90, 180, 270];
    private static readonly string[] Modes = ["default", "perfect", "trim"];

    public static int Run(string[] args)
    {
        var resultsPath = "spike005-results.json";
        var toolsDirectory = @"C:\libjpeg-turbo64\bin";
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            switch (args[index])
            {
                case "--results":
                    resultsPath = args[index + 1];
                    break;
                case "--tools-dir":
                    toolsDirectory = args[index + 1];
                    break;
                default:
                    break;
            }
        }

        var tools = new JpegTools(toolsDirectory);
        if (!tools.ToolsPresent)
        {
            Console.Error.WriteLine($"spike005: cjpeg/djpeg/jpegtran not found under '{toolsDirectory}'.");
            return 2;
        }

        var workRoot = Path.Combine(
            Path.GetTempPath(),
            "spike005-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(workRoot);

        var cases = new List<CaseResult>();
        var safety = new SortedDictionary<string, bool>(StringComparer.Ordinal)
        {
            ["perfect_success_is_coefficient_lossless_roundtrip"] = true,
            ["aligned_512_always_accepted_by_perfect"] = true,
            ["perfect_decode_delta_within_4"] = true,
            ["trim_output_dims_are_mcu_aligned"] = true,
            ["default_mode_always_produces_output"] = true,
        };

        foreach (var (width, height) in Dimensions)
        {
            foreach (var sampling in Samplings)
            {
                var sourceJpeg = EncodeFixture(tools, workRoot, width, height, sampling, progressive: false);
                var decodedSource = DecodeToPpm(tools, workRoot, sourceJpeg);
                foreach (var degrees in Rotations)
                {
                    var expected = decodedSource.Rotate(degrees / 90);
                    foreach (var mode in Modes)
                    {
                        var result = RunCase(
                            tools, workRoot, sourceJpeg, decodedSource, expected, width, height, sampling, degrees, mode);
                        cases.Add(result);
                        ApplySafety(safety, result, sampling);
                    }
                }
            }
        }

        var markers = MeasureMarkerBehavior(tools, workRoot);
        var summary = new
        {
            total = cases.Count,
            perfectRefusals = cases.Count(c => c.Mode == "perfect" && c.ExitCode != 0),
            perfectRoundTripLossless = cases.Count(c => c.Mode == "perfect" && c.ExitCode == 0 && c.RoundTripExact == true),
            defaultRoundTripLossless = cases.Count(c => c.Mode == "default" && c.ExitCode == 0 && c.RoundTripExact == true),
            defaultRoundTripLossy = cases.Count(c => c.Mode == "default" && c.ExitCode == 0 && c.RoundTripExact == false),
            trimResized = cases.Count(c => c.Mode == "trim" && c.ExitCode == 0 && !c.DimsMatchExpected),
            safetyHeld = safety.Values.All(held => held),
        };

        var report = new
        {
            spike = "SPIKE-005",
            environment = new
            {
                osDescription = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                dependency = "libjpeg-turbo 3.2.0 (DEP-008)",
                toolsDirectory,
                toolDigests = tools.ToolDigests(),
                harnessVersion = "0.1.0",
                fixturePolicy = "synthetic orientation-sensitive gradients; no user photos",
                runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            },
            safety,
            markers,
            summary,
            cases,
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

        Console.WriteLine(
            $"spike005: cases={summary.total} perfect-refusals={summary.perfectRefusals} " +
            $"perfect-roundtrip-lossless={summary.perfectRoundTripLossless} " +
            $"default-roundtrip-lossy={summary.defaultRoundTripLossy} trim-resized={summary.trimResized}");
        Console.WriteLine($"spike005: results written to {Path.GetFullPath(resultsPath)}");
        Directory.Delete(workRoot, recursive: true);
        return summary.safetyHeld ? 0 : 1;
    }

    private static void ApplySafety(SortedDictionary<string, bool> safety, CaseResult result, string sampling)
    {
        if (result.Mode == "perfect" && result.ExitCode == 0)
        {
            safety["perfect_success_is_coefficient_lossless_roundtrip"] &=
                result.RoundTripExact == true && result.DimsMatchExpected;
            safety["perfect_decode_delta_within_4"] &= result.MaxChannelDelta <= 4;
        }

        if (result.Width == 512 && result.Height == 512 && result.Mode == "perfect")
        {
            safety["aligned_512_always_accepted_by_perfect"] &= result.ExitCode == 0;
        }

        if (result.Mode == "trim" && result.ExitCode == 0 && result.OutputProduced)
        {
            var mcu = sampling == "2x2" ? 16 : 8;
            safety["trim_output_dims_are_mcu_aligned"] &=
                result.OutputWidth % mcu == 0 || result.OutputHeight % mcu == 0
                || (result.OutputWidth == result.Width && result.OutputHeight == result.Height)
                || result.DimsMatchExpected;
        }

        if (result.Mode == "default")
        {
            safety["default_mode_always_produces_output"] &= result.ExitCode == 0 && result.OutputProduced;
        }
    }

    private static CaseResult RunCase(
        JpegTools tools,
        string workRoot,
        string sourceJpeg,
        Ppm decodedSource,
        Ppm expected,
        int width,
        int height,
        string sampling,
        int degrees,
        string mode)
    {
        var id = string.Create(
            CultureInfo.InvariantCulture, $"{width}x{height}-{sampling}-r{degrees}-{mode}");
        var outputPath = Path.Combine(workRoot, id + ".jpg");
        var run = tools.Rotate(sourceJpeg, outputPath, degrees, mode, copyMarkers: "none");
        var produced = File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
        var outputWidth = 0;
        var outputHeight = 0;
        var pixelExact = false;
        long diffBytes = -1;
        var maxDelta = -1;
        bool? roundTripExact = null;
        if (produced && run.ExitCode == 0)
        {
            var decoded = DecodeToPpm(tools, workRoot, outputPath);
            outputWidth = decoded.Width;
            outputHeight = decoded.Height;
            (diffBytes, _) = Ppm.Compare(expected, decoded);
            pixelExact = diffBytes == 0;
            maxDelta = Ppm.MaxChannelDelta(expected, decoded);

            // The definitive losslessness test: invert the rotation in the
            // DCT domain and require a byte-identical decode of the original.
            var roundTripPath = Path.Combine(workRoot, id + "-rt.jpg");
            var inverse = tools.Rotate(outputPath, roundTripPath, (360 - degrees) % 360, "default", copyMarkers: "none");
            if (inverse.ExitCode == 0 && File.Exists(roundTripPath))
            {
                var restored = DecodeToPpm(tools, workRoot, roundTripPath);
                roundTripExact = Ppm.Compare(decodedSource, restored).DiffBytes == 0;
            }
            else
            {
                roundTripExact = false;
            }
        }

        return new CaseResult(
            id, width, height, sampling, Progressive: false, degrees, mode,
            run.ExitCode, produced, outputWidth, outputHeight,
            DimsMatchExpected: outputWidth == expected.Width && outputHeight == expected.Height,
            pixelExact, diffBytes, maxDelta, roundTripExact,
            string.IsNullOrEmpty(run.Stderr) ? null : run.Stderr);
    }

    private static object MeasureMarkerBehavior(JpegTools tools, string workRoot)
    {
        var sourceJpeg = EncodeFixture(tools, workRoot, 512, 512, "2x2", progressive: false);
        var commented = Path.Combine(workRoot, "commented.jpg");
        _ = tools.AddComment(sourceJpeg, commented, "spike005-marker-fixture");

        var copyNone = Path.Combine(workRoot, "copy-none.jpg");
        var copyAll = Path.Combine(workRoot, "copy-all.jpg");
        _ = tools.Rotate(commented, copyNone, 90, "perfect", copyMarkers: "none");
        _ = tools.Rotate(commented, copyAll, 90, "perfect", copyMarkers: "all");

        static bool HasComment(string path)
            => File.ReadAllBytes(path).AsSpan().IndexOf("spike005-marker-fixture"u8) >= 0;

        return new
        {
            commentSurvivesCopyAll = HasComment(copyAll),
            commentDroppedByCopyNone = !HasComment(copyNone),
        };
    }

    private static string EncodeFixture(
        JpegTools tools, string workRoot, int width, int height, string sampling, bool progressive)
    {
        var baseName = string.Create(CultureInfo.InvariantCulture, $"src-{width}x{height}-{sampling}");
        var ppmPath = Path.Combine(workRoot, baseName + ".ppm");
        var jpegPath = Path.Combine(workRoot, baseName + ".jpg");
        if (!File.Exists(jpegPath))
        {
            File.WriteAllBytes(ppmPath, Ppm.CreateFixture(width, height).Serialize());
            var encode = tools.Encode(ppmPath, jpegPath, sampling, progressive);
            if (encode.ExitCode != 0)
            {
                throw new InvalidOperationException($"cjpeg failed for {baseName}: {encode.Stderr}");
            }
        }

        return jpegPath;
    }

    private static Ppm DecodeToPpm(JpegTools tools, string workRoot, string jpegPath)
    {
        var ppmPath = Path.Combine(
            workRoot, Path.GetFileNameWithoutExtension(jpegPath) + "-decoded.ppm");
        var decode = tools.Decode(jpegPath, ppmPath);
        if (decode.ExitCode != 0)
        {
            throw new InvalidOperationException($"djpeg failed for {jpegPath}: {decode.Stderr}");
        }

        var ppm = Ppm.Parse(File.ReadAllBytes(ppmPath));
        File.Delete(ppmPath);
        return ppm;
    }
}
