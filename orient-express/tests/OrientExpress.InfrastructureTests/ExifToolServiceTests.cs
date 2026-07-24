using OrientExpress.Application.Filesystem;
using OrientExpress.Infrastructure.Metadata.ExifTool;

namespace OrientExpress.InfrastructureTests;

/// <summary>
/// WP-005 adapter integration tests. They exercise the real tool locally and
/// skip on machines without ExifTool (CI runners) — the SPIKE-004 evidence
/// pack remains the deep round-trip record.
/// </summary>
public sealed class ExifToolServiceTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-exiftool-tests-").FullName;

    private static string? Locate()
        => ExifToolLocator.Locate(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PATH"] = Environment.GetEnvironmentVariable("PATH") ?? string.Empty,
        });

    [Fact]
    public async Task Absent_tool_degrades_to_typed_capability_errors()
    {
        using var service = new ExifToolMetadataService(null);
        Assert.Equal("EXIFTOOL_NOT_FOUND", service.Identify().Error!.Code);
        var read = await service.ReadAsync(
            PathCanonicalizer.Normalize(Path.Combine(_directory, "x.jpg")), CancellationToken.None);
        Assert.Equal("EXIFTOOL_NOT_FOUND", read.Error!.Code);
    }

    [Fact]
    public async Task Orientation_write_is_range_guarded_before_any_tool_use()
    {
        using var service = new ExifToolMetadataService(null);
        var result = await service.WriteOrientationAsync(
            PathCanonicalizer.Normalize(Path.Combine(_directory, "x.jpg")), 9, CancellationToken.None);
        Assert.Equal("ORIENTATION_OUT_OF_RANGE", result.Error!.Code);
    }

    [Fact]
    public async Task Identify_read_write_and_diff_work_against_the_real_tool()
    {
        var toolPath = Locate();
        if (toolPath is null)
        {
            Assert.Skip("ExifTool is not installed on this machine");
        }

        var cjpeg = @"C:\libjpeg-turbo64\bin\cjpeg.exe";
        if (!File.Exists(cjpeg))
        {
            Assert.Skip("cjpeg (libjpeg-turbo) is not installed on this machine");
        }

        var ppm = Path.Combine(_directory, "f.ppm");
        var jpeg = Path.Combine(_directory, "f.jpg");
        File.WriteAllBytes(ppm, "P6\n8 8\n255\n"u8.ToArray().Concat(new byte[192]).ToArray());
        using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = cjpeg,
            ArgumentList = { "-outfile", jpeg, ppm },
            UseShellExecute = false,
            RedirectStandardError = true,
        }))
        {
            process!.WaitForExit(30_000);
        }

        using var service = new ExifToolMetadataService(toolPath);
        var identity = service.Identify().Require();
        Assert.NotEmpty(identity.Version);
        Assert.Equal(64, identity.Sha256.Length);

        var path = PathCanonicalizer.Normalize(jpeg);
        var before = (await service.ReadAsync(path, CancellationToken.None)).Require();
        Assert.Null(before.Orientation);

        Assert.True((await service.WriteOrientationAsync(path, 6, CancellationToken.None)).Require());
        var after = (await service.ReadAsync(path, CancellationToken.None)).Require();
        Assert.Equal(6, after.Orientation);

        var diff = (await service.CompareAsync(before, path, CancellationToken.None)).Require();
        Assert.Contains("IFD0:Orientation", diff.Added);
        Assert.Empty(diff.Removed);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
