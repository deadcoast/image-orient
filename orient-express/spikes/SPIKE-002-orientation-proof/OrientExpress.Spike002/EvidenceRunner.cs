using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OrientExpress.Spike002;

/// <summary>
/// SPIKE-003 evidence (runtime qualification: load, latency, determinism,
/// package footprint, provider policy) and the SPIKE-002 pilot (the
/// four-candidate rotation decision on a digest-pinned public-domain
/// portrait). The ≥99% precision gate for SPIKE-002 awaits the corpus
/// tranche; this run proves the mechanics end to end.
/// </summary>
public static class EvidenceRunner
{
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    public static int Run(string[] args)
    {
        var resultsPath = "spike002-003-results.json";
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

        var modelPath = Artifacts.Require(Artifacts.YuNetModel);
        var portraitPath = Artifacts.Require(Artifacts.PilotPortrait);

        var runtime = RuntimeEvidence(modelPath);
        var pilot = PilotEvidence(modelPath, portraitPath, toolsDirectory);

        var safety = new SortedDictionary<string, bool>(StringComparer.Ordinal)
        {
            ["model_digest_verified"] = true,
            ["inference_deterministic_within_and_across_sessions"] = (bool)runtime["deterministic"],
            ["upright_face_detected_on_pilot"] = (bool)pilot["uprightFaceDetected"],
            ["all_four_rotations_recovered"] = (bool)pilot["allRotationsRecovered"],
            ["geometric_check_agrees"] = (bool)pilot["geometricCheckAgrees"],
        };

        var report = new
        {
            spikes = new[] { "SPIKE-003", "SPIKE-002 (pilot)" },
            environment = new
            {
                osDescription = RuntimeInformation.OSDescription,
                framework = RuntimeInformation.FrameworkDescription,
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                onnxRuntime = "Microsoft.ML.OnnxRuntime 1.27.1 (DEP-005), CPU execution provider",
                model = Artifacts.YuNetModel,
                pilotImage = Artifacts.PilotPortrait,
                harnessVersion = "0.1.0",
                runUtc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            },
            safety,
            runtime,
            pilot,
            summary = new { safetyHeld = safety.Values.All(held => held) },
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

        Console.WriteLine($"spike002/003: results written to {Path.GetFullPath(resultsPath)}");
        return safety.Values.All(held => held) ? 0 : 1;
    }

    private static SortedDictionary<string, object> RuntimeEvidence(string modelPath)
    {
        var evidence = new SortedDictionary<string, object>(StringComparer.Ordinal);
        var syntheticInput = new float[3 * YuNetDetector.InputSize * YuNetDetector.InputSize];
        var seed = 12345u;
        for (var index = 0; index < syntheticInput.Length; index++)
        {
            seed = (seed * 1664525u) + 1013904223u;
            syntheticInput[index] = seed % 256;
        }

        var load = Stopwatch.StartNew();
        using var detector = new YuNetDetector(modelPath);
        load.Stop();
        evidence["modelLoadMs"] = load.ElapsedMilliseconds;
        evidence["inputName"] = detector.InputName;

        var cold = Stopwatch.StartNew();
        var first = detector.RunRaw(syntheticInput);
        cold.Stop();
        evidence["coldInferenceMs"] = cold.ElapsedMilliseconds;
        evidence["outputHeads"] = first.Count;

        var latencies = new List<double>();
        for (var run = 0; run < 50; run++)
        {
            var timer = Stopwatch.StartNew();
            _ = detector.RunRaw(syntheticInput);
            timer.Stop();
            latencies.Add(timer.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();
        evidence["warmLatencyMedianMs"] = Math.Round(latencies[latencies.Count / 2], 2);
        evidence["warmLatencyP95Ms"] = Math.Round(latencies[(int)(latencies.Count * 0.95)], 2);

        var second = detector.RunRaw(syntheticInput);
        using var secondSession = new YuNetDetector(modelPath);
        var otherSession = secondSession.RunRaw(syntheticInput);
        var deterministic = first.All(head =>
            head.Value.AsSpan().SequenceEqual(second[head.Key])
            && head.Value.AsSpan().SequenceEqual(otherSession[head.Key]));
        evidence["deterministic"] = deterministic;

        var nativeDll = Directory
            .GetFiles(AppContext.BaseDirectory, "onnxruntime.dll", SearchOption.AllDirectories)
            .FirstOrDefault();
        evidence["nativeRuntimeBytes"] = nativeDll is null ? 0L : new FileInfo(nativeDll).Length;
        evidence["providerPolicy"] =
            "CPU EP baseline (deterministic, offline, zero extra native deps); "
            + "DirectML and Windows ML remain gated alternatives pending WP-021 hardware benchmarks";
        return evidence;
    }

    private static SortedDictionary<string, object> PilotEvidence(
        string modelPath, string portraitPath, string toolsDirectory)
    {
        var evidence = new SortedDictionary<string, object>(StringComparer.Ordinal);
        using var detector = new YuNetDetector(modelPath);
        var portrait = PixelImage.LoadJpeg(portraitPath, toolsDirectory);
        evidence["pilotImageDims"] = $"{portrait.Width}x{portrait.Height}";

        var upright = detector.Detect(portrait);
        evidence["uprightFaces"] = upright.Count;
        evidence["uprightTopScore"] = upright.Count > 0 ? Math.Round(upright[0].Score, 4) : 0;
        evidence["uprightFaceDetected"] = upright.Count > 0 && upright[0].Score >= 0.8f;

        // The four-candidate approach: present the image at each rotation and
        // pick the counter-rotation whose detection score is highest.
        var cases = new List<object>();
        var allRecovered = true;
        var geometricAgrees = true;
        foreach (var presented in new[] { 0, 1, 2, 3 })
        {
            var presentedImage = portrait.Rotate(presented);
            var candidateScores = new double[4];
            FaceDetection? bestFace = null;
            var bestCandidate = -1;
            var bestScore = -1f;
            for (var candidate = 0; candidate < 4; candidate++)
            {
                var restored = presentedImage.Rotate(candidate);
                var faces = detector.Detect(restored, scoreThreshold: 0.5f);
                var score = faces.Count > 0 ? faces[0].Score : 0;
                candidateScores[candidate] = Math.Round(score, 4);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = candidate;
                    bestFace = faces.Count > 0 ? faces[0] : null;
                }
            }

            var expectedCandidate = (4 - presented) % 4;
            var recovered = bestCandidate == expectedCandidate;
            allRecovered &= recovered;
            var eyesAboveMouth = bestFace?.EyesAboveMouth ?? false;
            geometricAgrees &= eyesAboveMouth;
            var ordered = candidateScores.OrderDescending().ToArray();
            cases.Add(new
            {
                presentedRotation = presented * 90,
                expectedCandidate = expectedCandidate * 90,
                chosenCandidate = bestCandidate * 90,
                recovered,
                candidateScores,
                margin = Math.Round(ordered[0] - ordered[1], 4),
                eyesAboveMouthInChosen = eyesAboveMouth,
            });
        }

        evidence["fourRotationCases"] = cases;
        evidence["allRotationsRecovered"] = allRecovered;
        evidence["geometricCheckAgrees"] = geometricAgrees;
        return evidence;
    }
}
