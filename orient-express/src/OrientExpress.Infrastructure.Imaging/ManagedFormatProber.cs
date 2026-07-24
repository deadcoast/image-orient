using System.Buffers.Binary;
using System.Text;
using OrientExpress.Application.Metadata;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Imaging;

namespace OrientExpress.Infrastructure.Imaging;

/// <summary>
/// Bounded managed header probe (COMP-CODEC probe surface, WP-005). Content
/// signatures only — never the extension; JPEG SOF dimensions and EXIF IFD0
/// orientation in both TIFF endiannesses; PNG IHDR. Reads a capped prefix
/// with share-friendly handles; corrupt input yields warnings, never
/// exceptions (SPIKE-001-proven parser, productionized).
/// </summary>
public sealed class ManagedFormatProber : IImageFormatProber
{
    /// <summary>Metadata segments live at the front; 256 KiB covers real-world APP1/SOF placement.</summary>
    public const int ProbeBytes = 256 * 1024;

    public Result<ImageFormatProbe> Probe(NormalizedPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var stream = new FileStream(
                path.Extended, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[(int)Math.Min(stream.Length, ProbeBytes)];
            stream.ReadExactly(buffer);
            return Result.Ok(ProbeBytesSpan(buffer));
        }
        catch (FileNotFoundException)
        {
            return Result.Fail<ImageFormatProbe>(
                "FILE_NOT_FOUND", ErrorCategory.NotFound, false, $"no file at '{path.Display}'");
        }
        catch (DirectoryNotFoundException)
        {
            return Result.Fail<ImageFormatProbe>(
                "FILE_NOT_FOUND", ErrorCategory.NotFound, false, $"no file at '{path.Display}'");
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Fail<ImageFormatProbe>(
                "ACCESS_DENIED", ErrorCategory.AccessDenied, false, $"access denied to '{path.Display}'");
        }
        catch (IOException exception)
        {
            return Result.Fail<ImageFormatProbe>(
                "IO_ERROR", ErrorCategory.Unexpected, true, exception.Message);
        }
    }

    public static ImageFormatProbe ProbeBytesSpan(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            return new ImageFormatProbe("unknown", null, null, null, "shorter than 12 bytes");
        }

        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return ProbeJpeg(data);
        }

        if (data[..8].SequenceEqual((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ProbePng(data);
        }

        if (data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return new ImageFormatProbe("webp", null, null, null, null);
        }

        if (data[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = Encoding.ASCII.GetString(data[8..12]);
            return brand switch
            {
                "heic" or "heix" or "hevc" or "mif1" => new ImageFormatProbe("heic", null, null, null, null),
                "avif" or "avis" => new ImageFormatProbe("avif", null, null, null, null),
                _ => new ImageFormatProbe("iso-bmff-" + brand.Trim(), null, null, null, null),
            };
        }

        if ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00)
            || (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A))
        {
            return new ImageFormatProbe("tiff", null, null, null, null);
        }

        if (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8))
        {
            return new ImageFormatProbe("gif", null, null, null, null);
        }

        if (data[0] == 0x42 && data[1] == 0x4D)
        {
            return new ImageFormatProbe("bmp", null, null, null, null);
        }

        return new ImageFormatProbe("unknown", null, null, null, null);
    }

    private static ImageFormatProbe ProbePng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24 || !data[12..16].SequenceEqual("IHDR"u8))
        {
            return new ImageFormatProbe("png", null, null, null, "missing IHDR");
        }

        return new ImageFormatProbe(
            "png",
            (int)BinaryPrimitives.ReadUInt32BigEndian(data[16..20]),
            (int)BinaryPrimitives.ReadUInt32BigEndian(data[20..24]),
            null,
            null);
    }

    private static ImageFormatProbe ProbeJpeg(ReadOnlySpan<byte> data)
    {
        int? width = null;
        int? height = null;
        int? orientation = null;
        string? warning = null;

        var offset = 2;
        while (offset + 4 <= data.Length)
        {
            if (data[offset] != 0xFF)
            {
                warning = "segment desynchronization";
                break;
            }

            var marker = data[offset + 1];
            if (marker is 0xD9 or 0xDA)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data[(offset + 2)..]);
            if (length < 2 || offset + 2 + length > data.Length)
            {
                warning = "truncated segment";
                break;
            }

            var payload = data.Slice(offset + 4, length - 2);
            if (marker == 0xE1 && payload.Length > 6 && payload[..6].SequenceEqual("Exif\0\0"u8))
            {
                orientation = ParseTiffIfd0Orientation(payload[6..]);
            }
            else if (marker is 0xC0 or 0xC1 or 0xC2 && payload.Length >= 5)
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(payload[1..]);
                width = BinaryPrimitives.ReadUInt16BigEndian(payload[3..]);
            }

            offset += 2 + length;
        }

        return new ImageFormatProbe("jpeg", width, height, orientation, warning);
    }

    private static int? ParseTiffIfd0Orientation(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return null;
        }

        bool littleEndian;
        if (tiff[0] == 0x49 && tiff[1] == 0x49)
        {
            littleEndian = true;
        }
        else if (tiff[0] == 0x4D && tiff[1] == 0x4D)
        {
            littleEndian = false;
        }
        else
        {
            return null;
        }

        if (ReadUInt16(tiff, 2, littleEndian) != 42)
        {
            return null;
        }

        var ifdOffset = (int)ReadUInt32(tiff, 4, littleEndian);
        if (ifdOffset < 8 || ifdOffset + 2 > tiff.Length)
        {
            return null;
        }

        int entryCount = ReadUInt16(tiff, ifdOffset, littleEndian);
        for (var index = 0; index < entryCount; index++)
        {
            var entry = ifdOffset + 2 + (index * 12);
            if (entry + 12 > tiff.Length)
            {
                break;
            }

            int tag = ReadUInt16(tiff, entry, littleEndian);
            int type = ReadUInt16(tiff, entry + 2, littleEndian);
            var count = ReadUInt32(tiff, entry + 4, littleEndian);
            if (tag == 0x0112 && type == 3 && count == 1)
            {
                int value = ReadUInt16(tiff, entry + 8, littleEndian);
                return value is >= 1 and <= 8 ? value : null;
            }
        }

        return null;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset, bool littleEndian)
        => littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt16BigEndian(data[offset..]);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, int offset, bool littleEndian)
        => littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..])
            : BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
}
