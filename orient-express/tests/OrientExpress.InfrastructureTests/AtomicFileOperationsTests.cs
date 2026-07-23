using OrientExpress.Application.Filesystem;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Infrastructure.Windows;

namespace OrientExpress.InfrastructureTests;

/// <summary>WP-004 atomic primitives and capability classification on real NTFS.</summary>
public sealed class AtomicFileOperationsTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-atomic-tests-").FullName;

    private readonly WindowsAtomicFileOperations _operations = new();

    private NormalizedPath WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return PathCanonicalizer.Normalize(path);
    }

    [Fact]
    public void Replace_swaps_content_and_consumes_the_stage()
    {
        var target = WriteFile("target.bin", "original");
        var stage = StagePathPlanner.PlanStagePath(target);
        File.WriteAllText(stage.Display, "replacement");

        Assert.True(_operations.ReplaceExisting(target, stage).IsSuccess);
        Assert.Equal("replacement", File.ReadAllText(target.Display));
        Assert.False(File.Exists(stage.Display));
    }

    [Fact]
    public void Replace_on_missing_target_is_a_typed_not_found()
    {
        var stage = WriteFile("stage.bin", "replacement");
        var missing = PathCanonicalizer.Normalize(Path.Combine(_directory, "absent.bin"));

        var result = _operations.ReplaceExisting(missing, stage);
        Assert.False(result.IsSuccess);
        Assert.Equal("TARGET_NOT_FOUND", result.Error!.Code);
        Assert.True(File.Exists(stage.Display));
    }

    [Fact]
    public void Replace_on_locked_target_is_a_retryable_sharing_violation()
    {
        var target = WriteFile("locked.bin", "original");
        var stage = WriteFile("locked-stage.bin", "replacement");

        using var holder = new FileStream(target.Display, FileMode.Open, FileAccess.Read, FileShare.Read);
        var result = _operations.ReplaceExisting(target, stage);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCategory.SharingViolation, result.Error!.Category);
        Assert.True(result.Error!.IsRetryable);
    }

    [Fact]
    public void Move_to_new_destination_refuses_to_overwrite()
    {
        var source = WriteFile("move-src.bin", "payload");
        var destination = PathCanonicalizer.Normalize(Path.Combine(_directory, "move-dst.bin"));

        Assert.True(_operations.MoveToNewDestination(source, destination).IsSuccess);
        Assert.Equal("payload", File.ReadAllText(destination.Display));

        var second = WriteFile("move-src2.bin", "other");
        var refused = _operations.MoveToNewDestination(second, destination);
        Assert.False(refused.IsSuccess);
        Assert.Equal("DESTINATION_EXISTS", refused.Error!.Code);
        Assert.Equal("payload", File.ReadAllText(destination.Display));
    }

    [Fact]
    public void Stage_paths_are_same_directory_unique_and_marked()
    {
        var source = WriteFile("photo.jpg", "x");
        var first = StagePathPlanner.PlanStagePath(source);
        var second = StagePathPlanner.PlanStagePath(source);

        Assert.Equal(Path.GetDirectoryName(source.Display), Path.GetDirectoryName(first.Display));
        Assert.NotEqual(first.Display, second.Display);
        Assert.True(StagePathPlanner.LooksLikeStageFile(Path.GetFileName(first.Display)));
        Assert.False(StagePathPlanner.LooksLikeStageFile("photo.jpg"));
    }

    [Fact]
    public void Capability_probe_classifies_the_temp_volume_and_leaves_nothing_behind()
    {
        var before = Directory.GetFiles(_directory).Length;
        var result = new WindowsVolumeCapabilityProbe().Probe(PathCanonicalizer.Normalize(_directory));

        var capability = result.Require();
        Assert.Equal("NTFS", capability.FileSystemName);
        Assert.Equal(FilesystemCapabilityClass.SupportedNtfs, capability.Class);
        Assert.Equal(before, Directory.GetFiles(_directory).Length);
    }

    [Fact]
    public void Capability_probe_on_missing_directory_is_typed()
    {
        var result = new WindowsVolumeCapabilityProbe()
            .Probe(PathCanonicalizer.Normalize(Path.Combine(_directory, "absent")));
        Assert.False(result.IsSuccess);
        Assert.Equal("DIRECTORY_NOT_FOUND", result.Error!.Code);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
