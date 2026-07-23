using OrientExpress.Spike001;

namespace OrientExpress.Spike001.Tests;

public sealed class FormatSnifferTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    public void Jpeg_exif_orientation_parses_in_both_endiannesses(int orientation, bool bigEndian)
    {
        var probe = FormatSniffer.Probe(FixtureFactory.Jpeg(orientation, 4000, 3000, bigEndian));

        Assert.Equal("jpeg", probe.Format);
        Assert.Equal(orientation, probe.ExifOrientation);
        Assert.Equal(4000, probe.Width);
        Assert.Equal(3000, probe.Height);
        Assert.Equal("SpikeCam", probe.CameraMake);
        Assert.Null(probe.Warning);
    }

    [Fact]
    public void Jpeg_without_exif_reports_dimensions_and_no_orientation()
    {
        var probe = FormatSniffer.Probe(FixtureFactory.JpegWithoutExif(800, 600));

        Assert.Equal("jpeg", probe.Format);
        Assert.Null(probe.ExifOrientation);
        Assert.Equal(800, probe.Width);
        Assert.Equal(600, probe.Height);
    }

    [Fact]
    public void Png_ihdr_dimensions_parse()
    {
        var probe = FormatSniffer.Probe(FixtureFactory.Png(640, 480));

        Assert.Equal("png", probe.Format);
        Assert.Equal(640, probe.Width);
        Assert.Equal(480, probe.Height);
    }

    [Fact]
    public void Heic_ftyp_brand_is_recognized()
        => Assert.Equal("heic", FormatSniffer.Probe(FixtureFactory.Heic()).Format);

    [Fact]
    public void Corrupt_jpeg_yields_warning_not_exception()
    {
        var probe = FormatSniffer.Probe(FixtureFactory.Corrupt());

        Assert.Equal("jpeg", probe.Format);
        Assert.NotNull(probe.Warning);
        Assert.Null(probe.ExifOrientation);
    }

    [Fact]
    public void Invalid_exif_orientation_values_are_discarded()
    {
        var jpeg = FixtureFactory.Jpeg(9, 100, 100);
        Assert.Null(FormatSniffer.Probe(jpeg).ExifOrientation);
    }

    [Theory]
    [InlineData(new byte[] { 0x42, 0x4D, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, "bmp")]
    [InlineData(new byte[] { 0x49, 0x49, 0x2A, 0x00, 0, 0, 0, 0, 0, 0, 0, 0 }, "tiff")]
    [InlineData(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, "unknown")]
    public void Signatures_classify_other_formats(byte[] header, string expected)
        => Assert.Equal(expected, FormatSniffer.Probe(header).Format);
}
