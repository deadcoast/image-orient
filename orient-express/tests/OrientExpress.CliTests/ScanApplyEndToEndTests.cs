using System.Diagnostics;
using System.Text.Json;
using OrientExpress.Cli.Hosting;
using OrientExpress.Contracts.Cli;
using OrientExpress.Infrastructure.Imaging;
using OrientExpress.Infrastructure.Metadata.ExifTool;

namespace OrientExpress.CliTests;

/// <summary>
/// The product's first end-to-end proof: `upright scan` seals a plan over a
/// real folder and `upright apply` produces rotated, verified, committed
/// safe-copy outputs — sources untouched (PRD-FR-007/008/011/012/013).
/// Requires libjpeg-turbo and ExifTool; skips honestly when absent.
/// </summary>
public sealed class ScanApplyEndToEndTests : IDisposable
{
    private const string ToolsDirectory = @"C:\libjpeg-turbo64\bin";

    private static readonly string[] CpuProviders = ["cpu"];

    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-e2e-tests-").FullName;

    private string SourceRoot => Path.Combine(_directory, "photos");

    private string OutputRoot => Path.Combine(_directory, "photos-upright");

    private HostEnvironment Environment()
        => new(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Path.Combine(_directory, "user", "config.json"),
            Path.Combine(_directory, "data"));

    private static (int ExitCode, string Stdout, string Stderr) Invoke(HostEnvironment environment, params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliHost.Invoke(args, stdout, stderr, hostEnvironment: environment);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private static void RequireTools()
    {
        if (!File.Exists(Path.Combine(ToolsDirectory, "jpegtran.exe"))
            || !File.Exists(Path.Combine(ToolsDirectory, "cjpeg.exe")))
        {
            Assert.Skip("libjpeg-turbo is not installed on this machine");
        }

        if (ExifToolLocator.Locate(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)) is null)
        {
            Assert.Skip("ExifTool is not installed on this machine");
        }
    }

    /// <summary>A real JPEG with the given stored EXIF orientation tag.</summary>
    private async Task<string> CreatePhotoAsync(string relative, int width, int height, int orientation)
    {
        var jpeg = Path.Combine(SourceRoot, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(jpeg)!);
        var ppm = jpeg + ".ppm";
        using (var stream = File.Create(ppm))
        {
            stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
            stream.Write(new byte[width * height * 3]);
        }

        using (var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(ToolsDirectory, "cjpeg.exe"),
            ArgumentList = { "-outfile", jpeg, ppm },
            UseShellExecute = false,
            RedirectStandardError = true,
        }))
        {
            Assert.True(process!.WaitForExit(30_000));
        }

        File.Delete(ppm);
        if (orientation != 1)
        {
            using var service = new ExifToolMetadataService(
                ExifToolLocator.Locate(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
            var write = await service.WriteOrientationAsync(
                OrientExpress.Application.Filesystem.PathCanonicalizer.Normalize(jpeg),
                orientation,
                CancellationToken.None);
            Assert.True(write.IsSuccess);
        }

        return jpeg;
    }

    private static string JobIdFrom(string scanStdout)
    {
        using var document = JsonDocument.Parse(scanStdout);
        return document.RootElement.GetProperty("jobId").GetString()!;
    }

    [Fact]
    public async Task Scan_seals_a_deterministic_plan_and_apply_produces_verified_upright_copies()
    {
        RequireTools();
        var environment = Environment();

        // One sideways photo (tag 6 = needs 90 cw), one upside down (tag 3),
        // one already upright, one junk file that only pretends to be a JPEG.
        _ = await CreatePhotoAsync("a/sideways.jpg", 96, 64, orientation: 6);
        _ = await CreatePhotoAsync("upside-down.jpg", 96, 64, orientation: 3);
        _ = await CreatePhotoAsync("fine.jpg", 96, 64, orientation: 1);
        File.WriteAllBytes(Path.Combine(SourceRoot, "junk.jpg"), new byte[32]);

        var scan = Invoke(environment, "scan", SourceRoot, "--output", OutputRoot, "--json");
        Assert.Equal(0, scan.ExitCode);
        using (var document = JsonDocument.Parse(scan.Stdout))
        {
            var counts = document.RootElement.GetProperty("counts");
            Assert.Equal(4, counts.GetProperty("planned").GetInt64());
            Assert.Equal(2, counts.GetProperty("normalize").GetInt64());
            Assert.Equal(1, counts.GetProperty("keep").GetInt64());
            Assert.Equal(1, counts.GetProperty("unsupported").GetInt64());
        }

        // AC-014: scan wrote nothing anywhere near the sources.
        Assert.False(Directory.Exists(OutputRoot));

        var jobId = JobIdFrom(scan.Stdout);
        var apply = Invoke(environment, "apply", jobId, "--json");
        Assert.Equal(0, apply.ExitCode);
        using (var document = JsonDocument.Parse(apply.Stdout))
        {
            Assert.Equal(2, document.RootElement.GetProperty("counts").GetProperty("applied").GetInt64());
            Assert.Equal(0, document.RootElement.GetProperty("counts").GetProperty("failed").GetInt64());
        }

        // The sideways photo (96x64 tag 6) must now be 64x96 with tag 1 —
        // physically upright, verified through the independent prober.
        var rotated = ManagedFormatProber.ProbeBytesSpan(
            File.ReadAllBytes(Path.Combine(OutputRoot, "a", "sideways.jpg")));
        Assert.Equal("jpeg", rotated.Format);
        Assert.Equal(64, rotated.Width);
        Assert.Equal(96, rotated.Height);
        Assert.Equal(1, rotated.ExifOrientation ?? 1);

        var flipped = ManagedFormatProber.ProbeBytesSpan(
            File.ReadAllBytes(Path.Combine(OutputRoot, "upside-down.jpg")));
        Assert.Equal(96, flipped.Width);
        Assert.Equal(64, flipped.Height);

        // Keep/unsupported rows produce no outputs; sources stay untouched.
        Assert.False(File.Exists(Path.Combine(OutputRoot, "fine.jpg")));
        Assert.False(File.Exists(Path.Combine(OutputRoot, "junk.jpg")));
        var sourceProbe = ManagedFormatProber.ProbeBytesSpan(
            File.ReadAllBytes(Path.Combine(SourceRoot, "a", "sideways.jpg")));
        Assert.Equal(6, sourceProbe.ExifOrientation);

        // AC-028: replaying with --resume duplicates nothing and says so.
        var resume = Invoke(environment, "apply", jobId, "--resume", "--json");
        Assert.Equal((int)CliExitCode.NothingToDo, resume.ExitCode);

        // WP-013 command surface: committed outputs re-verify long after
        // apply; corrupting one is detected through the independent reopen.
        Assert.Equal(0, Invoke(environment, "verify", jobId).ExitCode);
        File.WriteAllBytes(Path.Combine(OutputRoot, "upside-down.jpg"), new byte[16]);
        var reverify = Invoke(environment, "verify", jobId, "--json");
        Assert.Equal((int)CliExitCode.VerificationFailed, reverify.ExitCode);
        Assert.Contains("OUTPUT_WRONG_FORMAT", reverify.Stdout, StringComparison.Ordinal);
        using (var document = JsonDocument.Parse(resume.Stdout))
        {
            Assert.Equal(0, document.RootElement.GetProperty("counts").GetProperty("applied").GetInt64());
            Assert.Equal(2, document.RootElement.GetProperty("counts").GetProperty("resumeSkipped").GetInt64());
        }
    }

    [Fact]
    public async Task Scan_with_detect_rotates_a_sideways_clean_tag_photo_via_the_face_model()
    {
        RequireTools();
        var fixtures = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            "orient-express", "spike-fixtures");
        var modelPath = Path.Combine(fixtures, "yunet.onnx");
        var portraitPath = Path.Combine(fixtures, "portrait1.jpg");
        if (!File.Exists(modelPath) || !File.Exists(portraitPath))
        {
            Assert.Skip("the digest-pinned YuNet model / pilot portrait are not cached on this machine");
        }

        var environment = Environment();

        // Real photos are rarely MCU-aligned; the operator picks the
        // reversible edge policy instead of the strict default (ADR-0007).
        _ = Directory.CreateDirectory(Path.Combine(_directory, "user"));
        File.WriteAllText(
            Path.Combine(_directory, "user", "config.json"),
            """{ "output": { "jpegEdge": "preserve-partial" } }""");

        // Install and activate the model through the CLI itself.
        var manifestPath = Path.Combine(_directory, "yunet-manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new
        {
            schemaVersion = "1.0",
            modelId = "yunet-baseline",
            version = "2023mar",
            task = "face-detection-five-landmarks",
            artifact = new
            {
                file = "model.onnx",
                sha256 = Convert.ToHexStringLower(
                    System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(modelPath))),
                bytes = new FileInfo(modelPath).Length,
                format = "onnx",
            },
            inputs = new[] { new { name = "input" } },
            outputs = new[] { new { name = "cls_8" } },
            preprocessing = new { id = "yunet-640-bgr", colorOrder = "BGR", resize = "stretch" },
            postprocessing = new { id = "yunet-fdyn-nms" },
            compatibility = new { appVersion = ">=0.1.0", runtimeApi = "onnxruntime", allowedProviders = CpuProviders },
            provenance = "OpenCV Zoo face_detection_yunet_2023mar (test install from the pinned cache)",
            license = "MIT",
        }));
        Assert.Equal(0, Invoke(environment, "models", "install", modelPath, "--manifest", manifestPath).ExitCode);
        Assert.Equal(0, Invoke(environment, "models", "activate", "yunet-baseline", "2023mar").ExitCode);

        // A real face photo stored sideways with a CLEAN tag — the case the
        // deterministic path cannot fix. Rotate the upright portrait 90 cw.
        _ = Directory.CreateDirectory(SourceRoot);
        var sideways = Path.Combine(SourceRoot, "sideways-face.jpg");
        using (var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(ToolsDirectory, "jpegtran.exe"),
            ArgumentList = { "-copy", "all", "-rotate", "90", "-outfile", sideways, portraitPath },
            UseShellExecute = false,
            RedirectStandardError = true,
        }))
        {
            Assert.True(process!.WaitForExit(30_000));
            Assert.Equal(0, process.ExitCode);
        }

        var original = ManagedFormatProber.ProbeBytesSpan(File.ReadAllBytes(portraitPath));
        var scan = Invoke(environment, "scan", SourceRoot, "--output", OutputRoot, "--detect", "--json");
        Assert.Equal(0, scan.ExitCode);
        using (var document = JsonDocument.Parse(scan.Stdout))
        {
            Assert.Equal(1, document.RootElement.GetProperty("counts").GetProperty("normalize").GetInt64());
            Assert.Contains("uncalibrated", document.RootElement.GetProperty("detection").GetString());
        }

        var apply = Invoke(environment, "apply", JobIdFrom(scan.Stdout), "--json");
        Assert.Equal(0, apply.ExitCode);

        // The output must be physically upright again: original dimensions.
        var fixedPhoto = ManagedFormatProber.ProbeBytesSpan(
            File.ReadAllBytes(Path.Combine(OutputRoot, "sideways-face.jpg")));
        Assert.Equal(original.Width, fixedPhoto.Width);
        Assert.Equal(original.Height, fixedPhoto.Height);
        Assert.Equal(1, fixedPhoto.ExifOrientation ?? 1);
    }

    [Fact]
    public async Task In_place_apply_rewrites_the_original_with_backup_and_undo_restores_it()
    {
        RequireTools();
        var environment = Environment();
        var photo = await CreatePhotoAsync("inplace.jpg", 96, 64, orientation: 6);
        var originalBytes = File.ReadAllBytes(photo);
        var backupRoot = Path.Combine(_directory, "backups");

        var scan = Invoke(
            environment, "scan", SourceRoot, "--output", OutputRoot,
            "--mode", "in-place", "--backup-root", backupRoot, "--json");
        Assert.Equal(0, scan.ExitCode);
        var jobId = JobIdFrom(scan.Stdout);

        // PRD-FR-010: no acknowledgement, no rewrite.
        var refused = Invoke(environment, "apply", jobId);
        Assert.Equal((int)CliExitCode.SafetyPreflightFailed, refused.ExitCode);
        Assert.Equal(originalBytes, File.ReadAllBytes(photo));

        var apply = Invoke(environment, "apply", jobId, "--acknowledge-in-place", "--json");
        Assert.Equal(0, apply.ExitCode);

        // The ORIGINAL file is now upright (64x96, tag 1); the backup is the
        // byte-exact pre-apply original.
        var rewritten = ManagedFormatProber.ProbeBytesSpan(File.ReadAllBytes(photo));
        Assert.Equal(64, rewritten.Width);
        Assert.Equal(96, rewritten.Height);
        Assert.Equal(1, rewritten.ExifOrientation ?? 1);
        Assert.Equal(originalBytes, File.ReadAllBytes(Path.Combine(backupRoot, "inplace.jpg")));

        // Undo needs its own confirmation, then restores byte-exactly.
        Assert.Equal((int)CliExitCode.SafetyPreflightFailed, Invoke(environment, "undo", jobId).ExitCode);
        Assert.Equal(0, Invoke(environment, "undo", jobId, "--yes").ExitCode);
        Assert.Equal(originalBytes, File.ReadAllBytes(photo));

        // Undo again: idempotent, nothing double-restored (AC-030).
        var again = Invoke(environment, "undo", jobId, "--yes", "--json");
        Assert.Equal(0, again.ExitCode);
        using var againDocument = JsonDocument.Parse(again.Stdout);
        Assert.Equal(0, againDocument.RootElement.GetProperty("counts").GetProperty("restored").GetInt64());
        Assert.Equal(1, againDocument.RootElement.GetProperty("counts").GetProperty("alreadyUndone").GetInt64());
    }

    [Fact]
    public async Task Undo_abstains_when_the_file_changed_after_apply()
    {
        RequireTools();
        var environment = Environment();
        var photo = await CreatePhotoAsync("conflict.jpg", 96, 64, orientation: 6);
        var backupRoot = Path.Combine(_directory, "backups");

        var scan = Invoke(
            environment, "scan", SourceRoot, "--output", OutputRoot,
            "--mode", "in-place", "--backup-root", backupRoot, "--json");
        var jobId = JobIdFrom(scan.Stdout);
        Assert.Equal(0, Invoke(environment, "apply", jobId, "--acknowledge-in-place").ExitCode);

        // Someone edits the file after apply: undo must refuse it (AC-029).
        var editedBytes = File.ReadAllBytes(photo).Concat(new byte[] { 0 }).ToArray();
        File.WriteAllBytes(photo, editedBytes);
        var undo = Invoke(environment, "undo", jobId, "--yes", "--json");
        Assert.Equal((int)CliExitCode.Conflict, undo.ExitCode);
        Assert.Contains("UNDO_STATE_MISMATCH", undo.Stdout, StringComparison.Ordinal);
        Assert.Equal(editedBytes, File.ReadAllBytes(photo));
    }

    [Fact]
    public async Task Apply_refuses_a_source_changed_after_scan_and_leaves_it_untouched()
    {
        RequireTools();
        var environment = Environment();
        var photo = await CreatePhotoAsync("changed.jpg", 96, 64, orientation: 6);

        var scan = Invoke(environment, "scan", SourceRoot, "--output", OutputRoot, "--json");
        Assert.Equal(0, scan.ExitCode);

        // Change the source after sealing: apply must refuse this row.
        await Task.Delay(50, TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(photo, DateTime.UtcNow.AddMinutes(5));

        var apply = Invoke(environment, "apply", JobIdFrom(scan.Stdout), "--json");
        Assert.Equal((int)CliExitCode.StalePlan, apply.ExitCode);
        using var document = JsonDocument.Parse(apply.Stdout);
        var failure = document.RootElement.GetProperty("failures").EnumerateArray().Single();
        Assert.Equal("PLAN_INPUT_STALE", failure.GetProperty("reason").GetString());
        Assert.False(File.Exists(Path.Combine(OutputRoot, "changed.jpg")));
        Assert.Equal(6, ManagedFormatProber.ProbeBytesSpan(File.ReadAllBytes(photo)).ExifOrientation);
    }

    [Fact]
    public async Task Apply_refuses_collisions_and_scan_with_no_files_says_nothing_to_do()
    {
        RequireTools();
        var environment = Environment();
        _ = await CreatePhotoAsync("c.jpg", 96, 64, orientation: 6);

        var scan = Invoke(environment, "scan", SourceRoot, "--output", OutputRoot, "--json");
        _ = Directory.CreateDirectory(OutputRoot);
        File.WriteAllBytes(Path.Combine(OutputRoot, "c.jpg"), [1]);

        var apply = Invoke(environment, "apply", JobIdFrom(scan.Stdout), "--json");
        Assert.Equal((int)CliExitCode.PartialCompletion, apply.ExitCode);
        Assert.Contains("DESTINATION_EXISTS", apply.Stdout, StringComparison.Ordinal);

        var empty = Invoke(
            environment, "scan", Directory.CreateDirectory(Path.Combine(_directory, "empty")).FullName,
            "--output", OutputRoot);
        Assert.Equal((int)CliExitCode.NothingToDo, empty.ExitCode);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
