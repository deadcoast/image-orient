using OrientExpress.Application.Filesystem;
using OrientExpress.Infrastructure.Imaging;

namespace OrientExpress.InfrastructureTests;

/// <summary>WP-005 bounded header prober (PRD-FR-003 read path).</summary>
public sealed class FormatProberTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-prober-tests-").FullName;

    private readonly ManagedFormatProber _prober = new();

    private static byte[] JpegWithOrientation(int orientation, bool bigEndian)
    {
        // SOI + APP1(Exif TIFF with orientation) + SOF0(64x48) + EOI.
        var tiff = new byte[26];
        tiff[0] = tiff[1] = bigEndian ? (byte)0x4D : (byte)0x49;
        void U16(int offset, ushort value)
        {
            if (bigEndian)
            {
                tiff[offset] = (byte)(value >> 8);
                tiff[offset + 1] = (byte)value;
            }
            else
            {
                tiff[offset] = (byte)value;
                tiff[offset + 1] = (byte)(value >> 8);
            }
        }

        U16(2, 42);
        tiff[bigEndian ? 7 : 4] = 8;
        U16(8, 1);
        U16(10, 0x0112);
        U16(12, 3);
        tiff[bigEndian ? 17 : 14] = 1;
        U16(18, (ushort)orientation);

        var app1Length = (ushort)(2 + 6 + tiff.Length);
        List<byte> jpeg = [0xFF, 0xD8, 0xFF, 0xE1, (byte)(app1Length >> 8), (byte)app1Length];
        jpeg.AddRange("Exif\0\0"u8.ToArray());
        jpeg.AddRange(tiff);
        jpeg.AddRange([0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x30, 0x00, 0x40, 0x01, 0x01, 0x11, 0x00]);
        jpeg.AddRange([0xFF, 0xD9]);
        return [.. jpeg];
    }

    private string WriteFixture(string name, byte[] content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(6, true)]
    [InlineData(8, false)]
    public void Jpeg_orientation_parses_in_both_endiannesses(int orientation, bool bigEndian)
    {
        var path = WriteFixture("o.jpg", JpegWithOrientation(orientation, bigEndian));
        var probe = _prober.Probe(PathCanonicalizer.Normalize(path)).Require();
        Assert.Equal("jpeg", probe.Format);
        Assert.Equal(orientation, probe.ExifOrientation);
        Assert.Equal(64, probe.Width);
        Assert.Equal(48, probe.Height);
    }

    [Fact]
    public void Png_dimensions_parse_and_signatures_classify()
    {
        byte[] png =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
            0x00, 0x00, 0x02, 0x80, 0x00, 0x00, 0x01, 0xE0, 0x08, 0x02, 0x00, 0x00, 0x00,
        ];
        var probe = _prober.Probe(PathCanonicalizer.Normalize(WriteFixture("a.png", png))).Require();
        Assert.Equal(("png", 640, 480), (probe.Format, probe.Width, probe.Height));

        byte[] heic = new byte[24];
        heic[3] = 24;
        "ftypheic"u8.ToArray().CopyTo(heic, 4);
        Assert.Equal("heic", _prober.Probe(PathCanonicalizer.Normalize(WriteFixture("b.heic", heic))).Require().Format);
        Assert.Equal(
            "unknown",
            _prober.Probe(PathCanonicalizer.Normalize(WriteFixture("c.bin", new byte[32]))).Require().Format);
    }

    [Fact]
    public void Truncated_jpeg_yields_warning_not_exception()
    {
        byte[] corrupt = [0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF, 0, 1, 2, 3, 4, 5, 6, 7];
        var probe = _prober.Probe(PathCanonicalizer.Normalize(WriteFixture("t.jpg", corrupt))).Require();
        Assert.Equal("jpeg", probe.Format);
        Assert.NotNull(probe.Warning);
    }

    [Fact]
    public void Missing_file_is_a_typed_not_found()
    {
        var result = _prober.Probe(PathCanonicalizer.Normalize(Path.Combine(_directory, "absent.jpg")));
        Assert.False(result.IsSuccess);
        Assert.Equal("FILE_NOT_FOUND", result.Error!.Code);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
