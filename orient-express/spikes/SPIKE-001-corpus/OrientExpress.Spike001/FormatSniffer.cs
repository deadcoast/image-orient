using System.Buffers.Binary;
using System.Text;

namespace OrientExpress.Spike001;

public sealed record ImageProbe(
    string Format,
    int? Width,
    int? Height,
    int? ExifOrientation,
    string? CameraMake,
    string? Warning);

/// <summary>
/// Bounded, allocation-light metadata probe: format signatures, JPEG SOF
/// dimensions, EXIF IFD0 orientation (0x0112) and camera make (0x010F), and
/// PNG IHDR dimensions. Reads only a capped prefix of each file; never
/// decodes pixels. Every offset is bounds-checked — a corrupt file yields a
/// warning, never an exception.
/// </summary>
public static class FormatSniffer
{
    /// <summary>Metadata segments live at the front; 256 KiB covers real-world APP1/SOF placement.</summary>
    public const int ProbeBytes = 256 * 1024;

    public static ImageProbe Probe(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            return new ImageProbe("unknown", null, null, null, null, "file shorter than 12 bytes");
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
            return new ImageProbe("webp", null, null, null, null, null);
        }

        if (data[4..8].SequenceEqual("ftyp"u8))
        {
            var brand = Encoding.ASCII.GetString(data[8..12]);
            return brand switch
            {
                "heic" or "heix" or "hevc" or "mif1" => new ImageProbe("heic", null, null, null, null, null),
                "avif" or "avis" => new ImageProbe("avif", null, null, null, null, null),
                _ => new ImageProbe("iso-bmff-" + brand.Trim(), null, null, null, null, null),
            };
        }

        if ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00)
            || (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A))
        {
            return new ImageProbe("tiff", null, null, null, null, null);
        }

        if (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8))
        {
            return new ImageProbe("gif", null, null, null, null, null);
        }

        if (data[0] == 0x42 && data[1] == 0x4D)
        {
            return new ImageProbe("bmp", null, null, null, null, null);
        }

        return new ImageProbe("unknown", null, null, null, null, null);
    }

    private static ImageProbe ProbePng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 24 || !data[12..16].SequenceEqual("IHDR"u8))
        {
            return new ImageProbe("png", null, null, null, null, "missing IHDR");
        }

        return new ImageProbe(
            "png",
            (int)BinaryPrimitives.ReadUInt32BigEndian(data[16..20]),
            (int)BinaryPrimitives.ReadUInt32BigEndian(data[20..24]),
            null,
            null,
            null);
    }

    private static ImageProbe ProbeJpeg(ReadOnlySpan<byte> data)
    {
        int? width = null;
        int? height = null;
        int? orientation = null;
        string? make = null;
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
            if (marker == 0xD9 || marker == 0xDA)
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
                (orientation, make) = ParseTiffIfd0(payload[6..]);
            }
            else if (marker is 0xC0 or 0xC1 or 0xC2 && payload.Length >= 5)
            {
                height = BinaryPrimitives.ReadUInt16BigEndian(payload[1..]);
                width = BinaryPrimitives.ReadUInt16BigEndian(payload[3..]);
            }

            offset += 2 + length;
        }

        return new ImageProbe("jpeg", width, height, orientation, make, warning);
    }

    private static (int? Orientation, string? Make) ParseTiffIfd0(ReadOnlySpan<byte> tiff)
    {
        if (tiff.Length < 8)
        {
            return (null, null);
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
            return (null, null);
        }

        if (ReadUInt16(tiff, 2, littleEndian) != 42)
        {
            return (null, null);
        }

        var ifdOffset = (int)ReadUInt32(tiff, 4, littleEndian);
        if (ifdOffset < 8 || ifdOffset + 2 > tiff.Length)
        {
            return (null, null);
        }

        int entryCount = ReadUInt16(tiff, ifdOffset, littleEndian);
        int? orientation = null;
        string? make = null;
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
                orientation = value is >= 1 and <= 8 ? value : null;
            }
            else if (tag == 0x010F && type == 2 && count is > 1 and <= 64)
            {
                var valueOffset = count <= 4 ? entry + 8 : (int)ReadUInt32(tiff, entry + 8, littleEndian);
                var end = valueOffset + (int)count;
                if (valueOffset >= 0 && end <= tiff.Length)
                {
                    make = Encoding.ASCII.GetString(tiff[valueOffset..end]).TrimEnd('\0').Trim();
                }
            }
        }

        return (orientation, make);
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
