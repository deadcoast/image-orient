using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrientExpress.Spike007;

public sealed record FormatProbeResult(
    string Format,
    bool EncodeSupported,
    bool DecodeSupported,
    bool RoundTripDimensionsExact,
    bool MetadataOrientationReadable,
    string? Diagnostic);

/// <summary>
/// Operational probes through WPF's WIC-backed imaging stack: encode a
/// synthetic bitmap, decode it back, compare dimensions, and (JPEG) read the
/// EXIF orientation WIC exposes via metadata queries. Operation-level truth,
/// not registry presence.
/// </summary>
public static class WpfProbes
{
    public static FormatProbeResult Probe(string format, string workDirectory)
    {
        BitmapEncoder? encoder = format switch
        {
            "jpeg" => new JpegBitmapEncoder(),
            "png" => new PngBitmapEncoder(),
            "tiff" => new TiffBitmapEncoder(),
            "bmp" => new BmpBitmapEncoder(),
            "gif" => new GifBitmapEncoder(),
            _ => null,
        };
        if (encoder is null)
        {
            return new FormatProbeResult(format, false, false, false, false, "no WPF/WIC encoder wrapper");
        }

        var path = Path.Combine(workDirectory, "probe." + format);
        try
        {
            var source = CreateSyntheticBitmap(48, 32);
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }

            using var readStream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(
                readStream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            var exact = frame.PixelWidth == 48 && frame.PixelHeight == 32;
            return new FormatProbeResult(format, true, true, exact, false, null);
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException or IOException)
        {
            return new FormatProbeResult(format, encoder is not null, false, false, false, exception.Message);
        }
    }

    /// <summary>Reads EXIF orientation from a JPEG through the WIC metadata query language.</summary>
    public static (bool Readable, int Value, string? Diagnostic) ReadJpegOrientation(string jpegPath)
    {
        try
        {
            using var stream = File.OpenRead(jpegPath);
            var decoder = BitmapDecoder.Create(
                stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames[0].Metadata is not BitmapMetadata metadata)
            {
                return (false, 0, "no metadata block");
            }

            var value = metadata.GetQuery("System.Photo.Orientation")
                ?? metadata.GetQuery("/app1/ifd/{ushort=274}");
            return value is null
                ? (false, 0, "orientation query returned null")
                : (true, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture), null);
        }
        catch (Exception exception) when (exception is NotSupportedException or FileFormatException or IOException)
        {
            return (false, 0, exception.Message);
        }
    }

    /// <summary>
    /// Presence probe that needs no sample file: hand the decoder a truncated
    /// real header and classify whether a codec ENGAGES (format/data error)
    /// or none exists (component-not-found). Operation-level truth; the WIC
    /// COM component enumerator proved unreliable from managed code.
    /// </summary>
    public static string ProbeHeaderEngagement(string format)
    {
        byte[] header = format switch
        {
            "heic" => FtypHeader("heic", "mif1"),
            "avif" => FtypHeader("avif", "avif"),
            "webp" =>
            [
                0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
                0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20,
            ],
            "jxl" => [0x00, 0x00, 0x00, 0x0C, 0x4A, 0x58, 0x4C, 0x20, 0x0D, 0x0A, 0x87, 0x0A],
            "jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00],
            "png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D],
            _ => [],
        };
        if (header.Length == 0)
        {
            return "unknown: NO_HEADER_TEMPLATE";
        }

        try
        {
            using var stream = new MemoryStream(header);
            _ = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            return "present: CODEC_DECODED_HEADER";
        }
        catch (NotSupportedException)
        {
            return "absent: CODEC_NOT_INSTALLED";
        }
        catch (FileFormatException)
        {
            return "present: CODEC_ENGAGED_TRUNCATED_SAMPLE_REJECTED";
        }
        catch (System.Runtime.InteropServices.COMException exception)
        {
            return (uint)exception.HResult == 0x88982F50
                ? "absent: CODEC_NOT_INSTALLED"
                : "present: CODEC_ENGAGED_HRESULT_" + ((uint)exception.HResult).ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (ArgumentException)
        {
            return "present: CODEC_ENGAGED_TRUNCATED_SAMPLE_REJECTED";
        }
    }

    private static byte[] FtypHeader(string brand, string compatible)
    {
        var header = new byte[24];
        header[3] = 24;
        System.Text.Encoding.ASCII.GetBytes("ftyp").CopyTo(header, 4);
        System.Text.Encoding.ASCII.GetBytes(brand).CopyTo(header, 8);
        System.Text.Encoding.ASCII.GetBytes(compatible).CopyTo(header, 16);
        return header;
    }

    private static BitmapSource CreateSyntheticBitmap(int width, int height)
    {
        var stride = width * 3;
        var pixels = new byte[height * stride];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * stride) + (x * 3);
                pixels[offset] = (byte)(x * 5);
                pixels[offset + 1] = (byte)(y * 7);
                pixels[offset + 2] = 128;
            }
        }

        return BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Rgb24, null, pixels, stride);
    }
}
