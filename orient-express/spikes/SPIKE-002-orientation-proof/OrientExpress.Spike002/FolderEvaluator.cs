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
        var showFailures = false;
        var debugEvidence = false;
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
                case "--show-failures":
                    showFailures = true;
                    index--;
                    break;
                case "--debug-evidence":
                    debugEvidence = true;
                    index--;
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

        var photos = new List<PhotoOutcome>();
        int skippedExif = 0, skippedNoFace = 0, unreadable = 0;
        foreach (var file in files)
        {
            if (photos.Count >= maxImages)
            {
                break;
            }

            PixelImage image;
            try
            {
                var exifOrientation = ReadExifOrientation(file);
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

            // The four rotations of the photo ARE the four restore candidates
            // for every presentation, so one pass per rotation covers all.
            var evidence = new CandidateEvidence[4];
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var faces = detector.Detect(image.Rotate(rotation), scoreThreshold: 0.3f);

                // Only clearly-detected faces (>= 0.6) vote in the sum;
                // weak 0.3-level detections would otherwise let several
                // false faces in a sideways copy out-vote one real face.
                evidence[rotation] = new CandidateEvidence(
                    faces.Count > 0 ? faces[0].Score : 0,
                    faces.Where(face => face.Score >= 0.6f).Sum(face => face.Score),
                    faces.Count,
                    faces.Count > 0 && faces[0].EyesAboveMouth);

                if (debugEvidence)
                {
                    var scoreList = string.Join(
                        " ",
                        faces.Select(face => face.Score.ToString("0.000", CultureInfo.InvariantCulture)
                            + (face.EyesAboveMouth ? "^" : "v")));
                    Console.Error.WriteLine(
                        $"debug photo {photos.Count + 1} rot{rotation * 90,3}: faces=[{scoreList}]");
                }
            }

            if (evidence[0].FaceCount == 0)
            {
                skippedNoFace++;
                continue;
            }

            var ruleA = DecisionRules.RuleA(evidence);
            var ruleB = DecisionRules.RuleB(evidence);
            photos.Add(new PhotoOutcome(
                photos.Count + 1,
                file,
                ruleA.Chosen == 0,
                (float)Math.Round(ruleA.Margin, 4),
                ruleB.Abstained ? "abstain" : ruleB.Chosen == 0 ? "correct" : "wrong",
                (float)Math.Round(ruleB.Margin, 4),
                ruleB.AbstainReason));

            if (photos.Count % 25 == 0)
            {
                Console.Error.WriteLine($"progress: {photos.Count} photos evaluated");
            }
        }

        if (photos.Count == 0)
        {
            Console.Error.WriteLine(
                "spike002: no evaluable photos (need upright JPEGs with EXIF orientation 1/absent and a detectable face).");
            return 2;
        }

        var ruleACorrect = photos.Count(p => p.RuleACorrect);
        var margins = photos.Select(p => p.RuleAMargin).Order().ToList();
        var abstention = MarginThresholds.ToDictionary(
            threshold => threshold.ToString("0.00", CultureInfo.InvariantCulture),
            threshold =>
            {
                var decided = photos.Where(p => p.RuleAMargin >= threshold).ToList();
                return new
                {
                    coveragePercent = Math.Round(100.0 * decided.Count / photos.Count, 2),
                    accuracyPercent = decided.Count == 0
                        ? 0
                        : Math.Round(100.0 * decided.Count(p => p.RuleACorrect) / decided.Count, 2),
                };
            });

        var ruleBDecided = photos.Where(p => p.RuleBOutcome != "abstain").ToList();
        var ruleBCorrect = ruleBDecided.Count(p => p.RuleBOutcome == "correct");
        var report = new
        {
            spike = "SPIKE-002 evaluation",
            runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            privacy = "aggregates and anonymous photo numbers only; no file names or paths are recorded",
            photosEvaluated = photos.Count,
            skipped = new { exifRotated = skippedExif, noFaceDetected = skippedNoFace, unreadable },
            ruleA_topFaceScore = new
            {
                correct = ruleACorrect,
                accuracyPercent = Math.Round(100.0 * ruleACorrect / photos.Count, 2),
                marginMedian = margins[margins.Count / 2],
                abstentionCurve = abstention,
            },
            ruleB_allFacesPlusGeometry = new
            {
                decided = ruleBDecided.Count,
                abstained = photos.Count - ruleBDecided.Count,
                coveragePercent = Math.Round(100.0 * ruleBDecided.Count / photos.Count, 2),
                correct = ruleBCorrect,
                accuracyOnDecidedPercent = ruleBDecided.Count == 0
                    ? 0
                    : Math.Round(100.0 * ruleBCorrect / ruleBDecided.Count, 2),
            },
            photos = photos.Select(p => new
            {
                p.PhotoNumber,
                p.RuleACorrect,
                p.RuleAMargin,
                p.RuleBOutcome,
                p.RuleBMargin,
                p.RuleBAbstainReason,
            }),
        };

        File.WriteAllText(resultsPath, JsonSerializer.Serialize(report, ReportOptions) + "\n");
        Console.WriteLine($"photos evaluated : {photos.Count} (skipped: {skippedExif} exif-rotated, {skippedNoFace} no face, {unreadable} unreadable)");
        Console.WriteLine($"rule A (best face score only):");
        Console.WriteLine($"  correct        : {ruleACorrect}/{photos.Count}  ({report.ruleA_topFaceScore.accuracyPercent}%)");
        foreach (var entry in abstention)
        {
            Console.WriteLine(
                $"  margin >= {entry.Key} : decides {entry.Value.coveragePercent}%, {entry.Value.accuracyPercent}% of those correct");
        }

        Console.WriteLine($"rule B (all faces vote + eye/mouth geometry check, abstains when unsure):");
        Console.WriteLine(
            $"  decides        : {ruleBDecided.Count}/{photos.Count}  ({report.ruleB_allFacesPlusGeometry.coveragePercent}%)");
        Console.WriteLine(
            $"  of those right : {ruleBCorrect}/{ruleBDecided.Count}  ({report.ruleB_allFacesPlusGeometry.accuracyOnDecidedPercent}%)");
        var abstainReasons = photos.Where(p => p.RuleBOutcome == "abstain")
            .GroupBy(p => p.RuleBAbstainReason ?? "unknown")
            .Select(g => $"{g.Key}: {g.Count()}");
        Console.WriteLine(
            $"  abstained      : {photos.Count - ruleBDecided.Count} (left for review, not rotated)"
            + (photos.Count - ruleBDecided.Count > 0 ? $"  [{string.Join(", ", abstainReasons)}]" : string.Empty));

        if (showFailures)
        {
            Console.WriteLine("problem photos (console only; not written to the results file):");
            foreach (var photo in photos.Where(p => !p.RuleACorrect || p.RuleBOutcome == "wrong"))
            {
                Console.WriteLine($"  WRONG   ruleA={(photo.RuleACorrect ? "ok" : "wrong")} ruleB={photo.RuleBOutcome} : {photo.FilePath}");
            }

            foreach (var photo in photos.Where(p => p.RuleACorrect && p.RuleBOutcome == "abstain"))
            {
                Console.WriteLine($"  ABSTAIN ruleA=ok : {photo.FilePath}");
            }
        }

        Console.WriteLine($"results written to {Path.GetFullPath(resultsPath)}");
        return 0;
    }

    private sealed record PhotoOutcome(
        int PhotoNumber,
        string FilePath,
        bool RuleACorrect,
        float RuleAMargin,
        string RuleBOutcome,
        float RuleBMargin,
        string? RuleBAbstainReason);

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
