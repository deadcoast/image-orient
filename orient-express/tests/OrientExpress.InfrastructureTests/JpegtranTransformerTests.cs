using System.Diagnostics;
using OrientExpress.Application.Filesystem;
using OrientExpress.Application.Metadata;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Metadata;
using OrientExpress.Contracts.Transform;
using OrientExpress.Infrastructure.Imaging;

namespace OrientExpress.InfrastructureTests;

/// <summary>
/// PRD-FR-011 engine behavior against the real jpegtran (skipped when
/// libjpeg-turbo is absent). Fixture sizes come from the SPIKE-005 evidence
/// matrix: 512x512 is always accepted by `-perfect`; 500x500 always
/// refuses every rotation. Losslessness of an accepted `-perfect` rotation
/// is proven by the inverse-rotation round-trip (never by decoded-pixel
/// comparison — SPIKE-005 binding).
/// </summary>
public sealed class JpegtranTransformerTests : IDisposable
{
    private const string ToolsDirectory = @"C:\libjpeg-turbo64\bin";

    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-transform-tests-").FullName;

    private readonly RecordingMetadataService _metadata = new();

    private static string JpegtranPath => Path.Combine(ToolsDirectory, "jpegtran.exe");

    private JpegtranTransformer Transformer() => new(JpegtranPath, _metadata);

    private static void RequireTools()
    {
        if (!File.Exists(JpegtranPath) || !File.Exists(Path.Combine(ToolsDirectory, "cjpeg.exe")))
        {
            Assert.Skip("libjpeg-turbo (jpegtran/cjpeg) is not installed on this machine");
        }
    }

    /// <summary>Encodes a black width-by-height JPEG via cjpeg.</summary>
    private string CreateJpeg(string name, int width, int height)
    {
        var ppm = Path.Combine(_directory, name + ".ppm");
        var jpeg = Path.Combine(_directory, name + ".jpg");
        var header = System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
        using (var stream = File.Create(ppm))
        {
            stream.Write(header);
            stream.Write(new byte[width * height * 3]);
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(ToolsDirectory, "cjpeg.exe"),
            ArgumentList = { "-quality", "90", "-outfile", jpeg, ppm },
            UseShellExecute = false,
            RedirectStandardError = true,
        });
        Assert.True(process!.WaitForExit(30_000));
        Assert.Equal(0, process.ExitCode);
        return jpeg;
    }

    private static FileSnapshot Snapshot(string path)
    {
        var normalized = PathCanonicalizer.Normalize(path);
        var info = new FileInfo(path);
        return new FileSnapshot(
            normalized,
            VolumeSerialHex: "0",
            FileIdHex: "0",
            LinkCount: 1,
            IsReparsePoint: false,
            IsCloudPlaceholder: false,
            IsOffline: false,
            new FileFingerprint(FingerprintStrength.Standard, info.Length, info.LastWriteTimeUtc.ToString("O"), "0", null),
            new FilesystemCapability(FilesystemCapabilityClass.SupportedNtfs, "NTFS", "0", "Fixed"));
    }

    private static StagingTarget Stage(string sourcePath)
        => new(StagePathPlanner.PlanStagePath(PathCanonicalizer.Normalize(sourcePath)));

    private static TransformPlan Plan(
        JpegOperation operation,
        JpegEdgePolicy edgePolicy = JpegEdgePolicy.Abort,
        int setOrientationTo = 1)
        => new(operation, edgePolicy, setOrientationTo, ExpectedWidth: 0, ExpectedHeight: 0);

    [Fact]
    public async Task Aligned_rotation_with_abort_policy_succeeds_and_round_trips_exactly()
    {
        RequireTools();
        var source = CreateJpeg("aligned", 512, 512);
        var stage = Stage(source);

        var artifact = (await Transformer().TransformAsync(
            Snapshot(source), stage, Plan(JpegOperation.Rotate90), CancellationToken.None)).Require();

        Assert.True(artifact.CoefficientLossless);
        Assert.True(artifact.OrientationTagWritten);
        Assert.Equal([(stage.Path.Display, 1)], _metadata.OrientationWrites);

        // AC-021: rotating the stage back must reproduce the source exactly.
        var inverse = Stage(source);
        var roundTrip = (await Transformer().TransformAsync(
            Snapshot(stage.Path.Display),
            inverse,
            Plan(JpegOperation.Rotate270, setOrientationTo: 0),
            CancellationToken.None)).Require();
        Assert.False(roundTrip.OrientationTagWritten);
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(inverse.Path.Display));
    }

    [Fact]
    public async Task Non_aligned_rotation_with_abort_policy_refuses_with_the_spike_named_code()
    {
        RequireTools();
        var source = CreateJpeg("edge", 500, 500);
        var stage = Stage(source);

        var result = await Transformer().TransformAsync(
            Snapshot(source), stage, Plan(JpegOperation.Rotate90), CancellationToken.None);

        // AC-022: the abort policy is a loud typed refusal, never silent.
        Assert.False(result.IsSuccess);
        Assert.Equal("JPEG_EDGE_NOT_PERFECT", result.Error!.Code);
        Assert.False(File.Exists(stage.Path.Display));
        Assert.Empty(_metadata.OrientationWrites);
    }

    [Fact]
    public async Task Non_aligned_rotation_with_preserve_partial_policy_transforms()
    {
        RequireTools();
        var source = CreateJpeg("partial", 500, 500);
        var stage = Stage(source);

        var artifact = (await Transformer().TransformAsync(
            Snapshot(source),
            stage,
            Plan(JpegOperation.Rotate90, JpegEdgePolicy.PreservePartial),
            CancellationToken.None)).Require();

        Assert.False(artifact.CoefficientLossless);
        Assert.True(File.Exists(stage.Path.Display));
    }

    [Fact]
    public async Task Operation_none_copies_and_writes_the_tag_without_jpegtran()
    {
        RequireTools();
        var source = CreateJpeg("noop", 96, 96);
        var stage = Stage(source);

        var artifact = (await new JpegtranTransformer(@"C:\does\not\exist\jpegtran.exe", _metadata)
            .TransformAsync(Snapshot(source), stage, Plan(JpegOperation.None), CancellationToken.None))
            .Require();

        Assert.True(artifact.CoefficientLossless);
        Assert.Equal(File.ReadAllBytes(source), File.ReadAllBytes(stage.Path.Display));
        Assert.Single(_metadata.OrientationWrites);
    }

    [Fact]
    public async Task Reencode_policy_is_a_typed_refusal_and_source_is_untouched()
    {
        RequireTools();
        var source = CreateJpeg("reencode", 96, 96);
        var before = File.ReadAllBytes(source);
        var stage = Stage(source);

        var result = await Transformer().TransformAsync(
            Snapshot(source),
            stage,
            Plan(JpegOperation.Rotate90, JpegEdgePolicy.Reencode),
            CancellationToken.None);

        Assert.Equal("TRANSFORM_REENCODE_UNAVAILABLE", result.Error!.Code);
        Assert.Equal(before, File.ReadAllBytes(source));
        Assert.False(File.Exists(stage.Path.Display));
    }

    [Fact]
    public async Task Non_jpeg_input_and_existing_stage_are_typed_errors()
    {
        var notJpeg = Path.Combine(_directory, "not.jpg");
        File.WriteAllBytes(notJpeg, [1, 2, 3, 4]);
        var result = await Transformer().TransformAsync(
            Snapshot(notJpeg), Stage(notJpeg), Plan(JpegOperation.Rotate90), CancellationToken.None);
        Assert.Equal("TRANSFORM_INPUT_NOT_JPEG", result.Error!.Code);

        var fakeJpeg = Path.Combine(_directory, "real.jpg");
        File.WriteAllBytes(fakeJpeg, [0xFF, 0xD8, 0xFF, 0xE0]);
        var occupiedStage = Stage(fakeJpeg);
        File.WriteAllBytes(occupiedStage.Path.Display, [0]);
        var conflict = await Transformer().TransformAsync(
            Snapshot(fakeJpeg), occupiedStage, Plan(JpegOperation.Rotate90), CancellationToken.None);
        Assert.Equal("TRANSFORM_STAGE_CONFLICT", conflict.Error!.Code);
    }

    [Fact]
    public async Task Metadata_write_failure_removes_the_stage_file()
    {
        RequireTools();
        _metadata.FailWrites = true;
        var source = CreateJpeg("mfail", 512, 512);
        var stage = Stage(source);

        var result = await Transformer().TransformAsync(
            Snapshot(source), stage, Plan(JpegOperation.Rotate90), CancellationToken.None);

        Assert.Equal("METADATA_WRITE_FAILED", result.Error!.Code);
        Assert.False(File.Exists(stage.Path.Display));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    /// <summary>Records orientation writes; the ExifTool interplay is covered by WP-005 tests.</summary>
    private sealed class RecordingMetadataService : IMetadataService
    {
        public List<(string Path, int Orientation)> OrientationWrites { get; } = [];

        public bool FailWrites { get; set; }

        public Result<(string Version, string Sha256)> Identify() => Result.Ok(("test", new string('0', 64)));

        public ValueTask<Result<MetadataSnapshot>> ReadAsync(NormalizedPath path, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public ValueTask<Result<bool>> WriteOrientationAsync(
            NormalizedPath path, int orientationValue, CancellationToken cancellationToken)
        {
            if (FailWrites)
            {
                return ValueTask.FromResult(Result.Fail<bool>(
                    "METADATA_WRITE_FAILED", ErrorCategory.Unexpected, false, "test failure"));
            }

            OrientationWrites.Add((path.Display, orientationValue));
            return ValueTask.FromResult(Result.Ok(true));
        }

        public ValueTask<Result<MetadataDiff>> CompareAsync(
            MetadataSnapshot before, NormalizedPath path, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
