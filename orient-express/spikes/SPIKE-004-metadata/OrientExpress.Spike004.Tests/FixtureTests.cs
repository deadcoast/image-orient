using OrientExpress.Spike004;

namespace OrientExpress.Spike004.Tests;

/// <summary>
/// CI-safe tests for the tool-independent parts of the SPIKE-004 harness
/// (no exiftool or cjpeg on CI runners; the evidence run is operator-driven).
/// </summary>
public sealed class FixtureTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("spike004-tests-").FullName;

    [Fact]
    public void Generated_png_is_structurally_valid()
    {
        var path = Fixtures.CreatePng(_directory);
        Assert.True(Fixtures.LooksLikeValidPng(path));
        Assert.True(new FileInfo(path).Length > 50);
    }

    [Fact]
    public void Png_validity_check_rejects_non_png_bytes()
    {
        var path = Path.Combine(_directory, "fake.png");
        File.WriteAllBytes(path, new byte[64]);
        Assert.False(Fixtures.LooksLikeValidPng(path));
    }

    [Fact]
    public void Jpeg_segment_inventory_walks_synthetic_markers()
    {
        var path = Path.Combine(_directory, "synthetic.jpg");
        // SOI, APP1 (len 4), DQT (len 4), SOF0 (len 4), SOS.
        File.WriteAllBytes(path,
        [
            0xFF, 0xD8,
            0xFF, 0xE1, 0x00, 0x04, 0x00, 0x00,
            0xFF, 0xDB, 0x00, 0x04, 0x00, 0x00,
            0xFF, 0xC0, 0x00, 0x04, 0x00, 0x00,
            0xFF, 0xDA, 0x00, 0x02,
        ]);

        var inventory = Fixtures.JpegSegmentInventory(path);
        Assert.Equal(1, inventory.GetValueOrDefault("APP1"));
        Assert.Equal(1, inventory.GetValueOrDefault("DQT"));
        Assert.Equal(1, inventory.GetValueOrDefault("SOF"));
        Assert.Equal(1, inventory.GetValueOrDefault("SOS"));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
