using OrientExpress.Application.Filesystem;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Infrastructure.Windows;

namespace OrientExpress.InfrastructureTests;

/// <summary>
/// Real-NTFS integration tests for COMP-FILESYSTEM (WP-004): identity,
/// fingerprints, drift detection, and policy refusals.
/// </summary>
public sealed class FileIdentityServiceTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-identity-tests-").FullName;

    private readonly WindowsFileIdentityService _service = new();

    private NormalizedPath WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return PathCanonicalizer.Normalize(path);
    }

    private async Task<FileSnapshot> CaptureAsync(
        NormalizedPath path, FingerprintStrength strength = FingerprintStrength.Strong)
        => (await _service.CaptureAsync(path, strength, CancellationToken.None)).Require();

    [Fact]
    public async Task Identity_is_stable_across_reopens_and_distinct_across_files()
    {
        var first = WriteFile("a.bin", [1, 2, 3]);
        var second = WriteFile("b.bin", [1, 2, 3]);

        var firstSnapshot = await CaptureAsync(first);
        Assert.Equal(firstSnapshot.FileIdHex, (await CaptureAsync(first)).FileIdHex);
        Assert.Equal(firstSnapshot.VolumeSerialHex, (await CaptureAsync(second)).VolumeSerialHex);
        Assert.NotEqual(firstSnapshot.FileIdHex, (await CaptureAsync(second)).FileIdHex);
    }

    [Fact]
    public async Task Snapshot_compare_matches_when_unchanged_and_reports_drift()
    {
        var path = WriteFile("c.bin", [1, 2, 3, 4]);
        var snapshot = await CaptureAsync(path);

        var unchanged = await _service.CompareAsync(snapshot, CancellationToken.None);
        Assert.True(unchanged.Require().Matches);

        File.WriteAllBytes(path.Display, [9, 9, 9, 9, 9]);
        var drifted = await _service.CompareAsync(snapshot, CancellationToken.None);
        Assert.False(drifted.Require().Matches);
        Assert.Contains("SIZE_CHANGED", drifted.Require().DriftReasons);

        File.Delete(path.Display);
        var missing = await _service.CompareAsync(snapshot, CancellationToken.None);
        Assert.Equal(["MISSING"], missing.Require().DriftReasons);
    }

    [Fact]
    public async Task Prefix_digest_catches_content_change_that_preserves_size_and_timestamp()
    {
        var path = WriteFile("d.bin", [1, 2, 3, 4]);
        var lastWrite = File.GetLastWriteTimeUtc(path.Display);
        var snapshot = await CaptureAsync(path, FingerprintStrength.Standard);

        File.WriteAllBytes(path.Display, [4, 3, 2, 1]);
        File.SetLastWriteTimeUtc(path.Display, lastWrite);

        var drifted = await _service.CompareAsync(snapshot, CancellationToken.None);
        Assert.False(drifted.Require().Matches);
        Assert.Contains("CONTENT_PREFIX_CHANGED", drifted.Require().DriftReasons);
        Assert.DoesNotContain("SIZE_CHANGED", drifted.Require().DriftReasons);
    }

    [Fact]
    public async Task Strong_fingerprint_includes_full_digest_and_standard_does_not()
    {
        var path = WriteFile("e.bin", [7, 7, 7]);
        Assert.NotNull((await CaptureAsync(path, FingerprintStrength.Strong)).Fingerprint.FullSha256);
        Assert.Null((await CaptureAsync(path, FingerprintStrength.Standard)).Fingerprint.FullSha256);
    }

    [Fact]
    public async Task Hardlink_aliases_share_identity_and_report_link_count()
    {
        var original = WriteFile("f.bin", [5, 5, 5]);
        var aliasPath = Path.Combine(_directory, "f-alias.bin");
        if (!WindowsHardLinks.TryCreate(aliasPath, original.Display))
        {
            Assert.Skip("hardlink creation unavailable on this volume");
        }

        var snapshot = await CaptureAsync(original);
        var alias = await CaptureAsync(PathCanonicalizer.Normalize(aliasPath));
        Assert.Equal(snapshot.FileIdHex, alias.FileIdHex);
        Assert.True(snapshot.LinkCount >= 2);
    }

    [Fact]
    public async Task Reparse_points_are_refused_by_policy()
    {
        var target = WriteFile("g.bin", [1]);
        var linkPath = Path.Combine(_directory, "g-link.bin");
        try
        {
            File.CreateSymbolicLink(linkPath, target.Display);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Skip("symbolic link creation requires developer mode or elevation");
        }

        var result = await _service.CaptureAsync(
            PathCanonicalizer.Normalize(linkPath), FingerprintStrength.Standard, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal("REPARSE_NOT_TRAVERSED", result.Error!.Code);
    }

    [Fact]
    public async Task Missing_file_is_a_typed_not_found_result()
    {
        var result = await _service.CaptureAsync(
            PathCanonicalizer.Normalize(Path.Combine(_directory, "absent.bin")),
            FingerprintStrength.Standard,
            CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal("FILE_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task Long_unicode_paths_round_trip_without_loss()
    {
        var deep = Path.Combine(_directory, string.Join('\\', Enumerable.Repeat("很長的目錄-ünïcode", 18)));
        Directory.CreateDirectory(PathCanonicalizer.ToExtended(deep));
        var filePath = Path.Combine(deep, "photo-fixture.bin");
        Assert.True(filePath.Length > 280, $"fixture path is only {filePath.Length} chars");

        File.WriteAllBytes(PathCanonicalizer.ToExtended(filePath), [1, 2, 3]);
        var snapshot = await CaptureAsync(PathCanonicalizer.Normalize(filePath));
        Assert.Equal(3, snapshot.Fingerprint.SizeBytes);
        Assert.Contains("很長的目錄-ünïcode", snapshot.Path.Display, StringComparison.Ordinal);
    }

    public void Dispose()
        => Directory.Delete(PathCanonicalizer.ToExtended(_directory), recursive: true);
}
