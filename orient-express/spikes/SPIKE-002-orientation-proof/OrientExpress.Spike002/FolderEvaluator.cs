using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;

namespace OrientExpress.Spike002;

/// <summary>
/// The operator-run corpus evaluation (SPIKE-002 exit gate): walks a folder
/// of ordinary JPEGs, and for every photo presents all four rotations to the
/// detector and checks whether the correct upright is recovered.
///
/// Read-only. Nothing is modified, copied, or uploaded; the results file
/// contains aggregates and anonymous per-case numbers — no file names or
/// paths. Photos whose EXIF orientation tag is not 1 are skipped and counted
/// (their stored pixels are not upright, so "as stored = upright" ground
/// truth would be wrong for them). Photos with no detectable face are
/// counted separately — they are future abstentions, not errors.
/// </summary>
public static class FolderEvaluator
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };
    private static readonly float[] MarginThresholds = [0f, 0.05f, 0.1f, 0.2f];

    public static int Run(string[] args)
    {
        string? imagesDirectory = null;
        var resultsPath = "spike002-evaluation.json";
        var toolsDirectory = @"C:\libjpeg-turbo64\bin";
        var maxImages = 250;
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            switch (args[index])
            {
                case "--images":
                    imagesDirectory = args[index + 1];
                    break;
                case "--results":
                    resultsPath = args[index + 1];
                    break;
                case "--tools-dir":
                    toolsDirectory = args[index + 1];
                    break;
                case "--max":
                    maxImages = int.Parse(args[index + 1], CultureInfo.InvariantCulture);
                    break;
                default:
                    break;
            }
        }

        if (imagesDirectory is null || !Directory.Exists(imagesDirectory))
        {
            Console.Error.WriteLine("spike002: pass --images <folder-with-jpegs>");
            return 2;
        }

        var modelPath = Artifacts.Require(Artifacts.YuNetModel);
        using var detector = new YuNetDetector(modelPath);

        var files = Directory
            .EnumerateFiles(imagesDirectory, "*.jp*g", SearchOption.AllDirectories)
            .Take(maxImages * 3)
            .ToList();

        var cases = new List<CaseOutcome>();
        int evaluated = 0, skippedExif = 0, skippedNoFace = 0, unreadable = 0;
        foreach (var file in files)
        {
            if (evaluated >= maxImages)
            {
                break;
            }

            int? exifOrientation;
            PixelImage image;
            try
            {
                exifOrientation = ReadExifOrientation(file);
                if (exifOrientation is not null && exifOrientation != 1)
                {
                    skippedExif++;
                    continue;
                }

                image = PixelImage.LoadJpeg(file, toolsDirectory);
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException or InvalidDataException)
            {
                unreadable++;
                continue;
            }

            // Downscale once so the four rotations are cheap; the detector
            // resizes to 640 internally anyway.
            var longEdge = Math.Max(image.Width, image.Height);
            if (longEdge > 960)
            {
                var scale = 960.0 / longEdge;
                image = image.ResizeBilinear(
                    Math.Max(64, (int)(image.Width * scale)),
                    Math.Max(64, (int)(image.Height * scale)));
            }

            var uprightFaces = detector.Detect(image, scoreThreshold: 0.5f);
            if (uprightFaces.Count == 0)
            {
                skippedNoFace++;
                continue;
            }

            evaluated++;
            for (var presented = 0; presented < 4; presented++)
            {
                var presentedImage = image.Rotate(presented);
                var scores = new float[4];
                var eyesAboveMouth = new bool[4];
                for (var candidate = 0; candidate < 4; candidate++)
                {
                    var faces = detector.Detect(presentedImage.Rotate(candidate), scoreThreshold: 0.3f);
                    scores[candidate] = faces.Count > 0 ? faces[0].Score : 0;
                    eyesAboveMouth[candidate] = faces.Count > 0 && faces[0].EyesAboveMouth;
                }

                var chosen = Array.IndexOf(scores, scores.Max());
                var expected = (4 - presented) % 4;
                var ordered = scores.OrderDescending().ToArray();
                cases.Add(new CaseOutcome(
                    evaluated,
                    presented * 90,
                    chosen == expected,
                    (float)Math.Round(ordered[0] - ordered[1], 4),
                    eyesAboveMouth[chosen]));
            }

            if (evaluated % 25 == 0)
            {
                Console.Error.WriteLine($"progress: {evaluated} photos evaluated");
            }
        }

        if (cases.Count == 0)
        {
            Console.Error.WriteLine(
                "spike002: no evaluable photos (need upright JPEGs with EXIF orientation 1/absent and a detectable face).");
            return 2;
        }

        var correct = cases.Count(c => c.Recovered);
        var abstention = MarginThresholds.ToDictionary(
            threshold => threshold.ToString("0.00", CultureInfo.InvariantCulture),
            threshold =>
            {
                var decided = cases.Where(c => c.Margin >= threshold).ToList();
                return new
                {
                    coveragePercent = Math.Round(100.0 * decided.Count / cases.Count, 2),
                    accuracyPercent = decided.Count == 0
                        ? 0
                        : Math.Round(100.0 * decided.Count(c => c.Recovered) / decided.Count, 2),
                };
            });

        var margins = cases.Select(c => c.Margin).Order().ToList();
        var report = new
        {
            spike = "SPIKE-002 evaluation",
            runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            privacy = "aggregates and anonymous case numbers only; no file names or paths are recorded",
            photosEvaluated = evaluated,
            skipped = new { exifRotated = skippedExif, noFaceDetected = skippedNoFace, unreadable },
            rotationCases = cases.Count,
            recovered = correct,
            accuracyPercent = Math.Round(100.0 * correct / cases.Count, 2),
            accuracyByPresentedRotation = cases.GroupBy(c => c.PresentedDegrees).OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key.ToString(CultureInfo.InvariantCulture),
                    g => Math.Round(100.0 * g.Count(c => c.Recovered) / g.Count(), 2)),
            marginMedian = margins[margins.Count / 2],
            marginP10 = margins[(int)(margins.Count * 0.10)],
            abstentionCurve = abstention,
            geometricAgreementPercent = Math.Round(
                100.0 * cases.Count(c => c.Recovered && c.EyesAboveMouthInChosen) / Math.Max(1, correct), 2),
            cases,
        };

        File.WriteAllText(resultsPath, JsonSerializer.Serialize(report, ReportOptions) + "\n");
        Console.WriteLine($"photos evaluated : {evaluated} (skipped: {skippedExif} exif-rotated, {skippedNoFace} no face, {unreadable} unreadable)");
        Console.WriteLine($"rotation tests   : {cases.Count}");
        Console.WriteLine($"correct          : {correct}  ({report.accuracyPercent}%)");
        foreach (var entry in report.accuracyByPresentedRotation)
        {
            Console.WriteLine($"  presented {entry.Key,3} deg : {entry.Value}%");
        }

        Console.WriteLine("with abstention (skip low-confidence instead of guessing):");
        foreach (var entry in abstention)
        {
            Console.WriteLine(
                $"  margin >= {entry.Key} : decides {entry.Value.coveragePercent}% of cases, {entry.Value.accuracyPercent}% of those correct");
        }

        Console.WriteLine($"results written to {Path.GetFullPath(resultsPath)}");
        return 0;
    }

    private sealed record CaseOutcome(
        int PhotoNumber, int PresentedDegrees, bool Recovered, float Margin, bool EyesAboveMouthInChosen);

    /// <summary>Minimal bounded EXIF IFD0 orientation read (both endiannesses).</summary>
    private static int? ReadExifOrientation(string jpegPath)
    {
        using var stream = File.OpenRead(jpegPath);
        var buffer = new byte[Math.Min(stream.Length, 256 * 1024)];
        stream.ReadExactly(buffer);
        var data = buffer.AsSpan();
        if (data.Length < 12 || data[0] != 0xFF || data[1] != 0xD8)
        {
            throw new InvalidDataException("not a JPEG");
        }

        var offset = 2;
        while (offset + 4 <= data.Length && data[offset] == 0xFF)
        {
            var marker = data[offset + 1];
            if (marker is 0xD9 or 0xDA)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 2)..]);
            if (length < 2 || offset + 2 + length > data.Length)
            {
                break;
            }

            var payload = data.Slice(offset + 4, length - 2);
            if (marker == 0xE1 && payload.Length > 14 && payload[..6].SequenceEqual("Exif\0\0"u8))
            {
                var tiffStart = offset + 4 + 6;
                var tiffLength = length - 2 - 6;
                var little = buffer[tiffStart] == 0x49;
                var ifd = (int)ReadU32(buffer, tiffStart + 4, little);
                if (ifd + 2 <= tiffLength)
                {
                    int count = ReadU16(buffer, tiffStart + ifd, little);
                    for (var entry = 0; entry < count; entry++)
                    {
                        var at = ifd + 2 + (entry * 12);
                        if (at + 12 > tiffLength)
                        {
                            break;
                        }

                        if (ReadU16(buffer, tiffStart + at, little) == 0x0112
                            && ReadU16(buffer, tiffStart + at + 2, little) == 3)
                        {
                            int value = ReadU16(buffer, tiffStart + at + 8, little);
                            return value is >= 1 and <= 8 ? value : null;
                        }
                    }
                }
            }

            offset += 2 + length;
        }

        return null;
    }

    private static ushort ReadU16(byte[] buffer, int at, bool littleEndian)
        => littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(at))
            : BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(at));

    private static uint ReadU32(byte[] buffer, int at, bool littleEndian)
        => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(at))
            : BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(at));
}
