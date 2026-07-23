using System.Buffers.Binary;
using System.Text;

namespace OrientExpress.Spike001;

/// <summary>
/// Builds tiny synthetic image files carrying exactly the metadata segments
/// the probe reads. Used by the self-test and unit tests so the scanner is
/// verifiable without any real photographs.
/// </summary>
public static class FixtureFactory
{
    public static byte[] Jpeg(int orientation, ushort width, ushort height, bool bigEndianExif = false, string make = "SpikeCam")
    {
        var exif = TiffWithOrientation(orientation, bigEndianExif, make);
        using var stream = new MemoryStream();
        stream.Write([0xFF, 0xD8]);

        // APP1 Exif segment.
        stream.Write([0xFF, 0xE1]);
        WriteUInt16BE(stream, (ushort)(2 + 6 + exif.Length));
        stream.Write("Exif\0\0"u8);
        stream.Write(exif);

        // SOF0: precision, height, width, one component.
        stream.Write([0xFF, 0xC0]);
        WriteUInt16BE(stream, 11);
        stream.WriteByte(8);
        WriteUInt16BE(stream, height);
        WriteUInt16BE(stream, width);
        stream.Write([0x01, 0x01, 0x11, 0x00]);

        stream.Write([0xFF, 0xD9]);
        return stream.ToArray();
    }

    public static byte[] JpegWithoutExif(ushort width, ushort height)
    {
        using var stream = new MemoryStream();
        stream.Write([0xFF, 0xD8]);
        stream.Write([0xFF, 0xC0]);
        WriteUInt16BE(stream, 11);
        stream.WriteByte(8);
        WriteUInt16BE(stream, height);
        WriteUInt16BE(stream, width);
        stream.Write([0x01, 0x01, 0x11, 0x00]);
        stream.Write([0xFF, 0xD9]);
        return stream.ToArray();
    }

    public static byte[] Png(uint width, uint height)
    {
        using var stream = new MemoryStream();
        stream.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteUInt32BE(stream, 13);
        stream.Write("IHDR"u8);
        WriteUInt32BE(stream, width);
        WriteUInt32BE(stream, height);
        stream.Write([8, 2, 0, 0, 0]);
        WriteUInt32BE(stream, 0);
        return stream.ToArray();
    }

    public static byte[] Heic()
    {
        using var stream = new MemoryStream();
        WriteUInt32BE(stream, 24);
        stream.Write("ftyp"u8);
        stream.Write("heic"u8);
        WriteUInt32BE(stream, 0);
        stream.Write("mif1"u8);
        stream.Write("heic"u8);
        return stream.ToArray();
    }

    public static byte[] Corrupt()
        => [0xFF, 0xD8, 0xFF, 0xE1, 0xFF, 0xFF, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09];

    private static byte[] TiffWithOrientation(int orientation, bool bigEndian, string make)
    {
        // Layout: header(8) + IFD0: count(2) + 2 entries(24) + next(4) = 38,
        // then the make string at offset 38.
        var makeBytes = Encoding.ASCII.GetBytes(make + "\0");
        var tiff = new byte[38 + makeBytes.Length];
        tiff[0] = tiff[1] = bigEndian ? (byte)0x4D : (byte)0x49;
        WriteUInt16(tiff, 2, 42, bigEndian);
        WriteUInt32(tiff, 4, 8, bigEndian);
        WriteUInt16(tiff, 8, 2, bigEndian);

        // Entry 0: Make (0x010F), ASCII, offset 38.
        WriteUInt16(tiff, 10, 0x010F, bigEndian);
        WriteUInt16(tiff, 12, 2, bigEndian);
        WriteUInt32(tiff, 14, (uint)makeBytes.Length, bigEndian);
        WriteUInt32(tiff, 18, 38, bigEndian);

        // Entry 1: Orientation (0x0112), SHORT, inline value.
        WriteUInt16(tiff, 22, 0x0112, bigEndian);
        WriteUInt16(tiff, 24, 3, bigEndian);
        WriteUInt32(tiff, 26, 1, bigEndian);
        WriteUInt16(tiff, 30, (ushort)orientation, bigEndian);

        WriteUInt32(tiff, 34, 0, bigEndian);
        makeBytes.CopyTo(tiff, 38);
        return tiff;
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value, bool bigEndian)
    {
        if (bigEndian)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(offset), value);
        }
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value, bool bigEndian)
    {
        if (bigEndian)
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset), value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(offset), value);
        }
    }

    private static void WriteUInt16BE(Stream stream, ushort value)
    {
        Span<byte> scratch = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(scratch, value);
        stream.Write(scratch);
    }

    private static void WriteUInt32BE(Stream stream, uint value)
    {
        Span<byte> scratch = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(scratch, value);
        stream.Write(scratch);
    }
}
