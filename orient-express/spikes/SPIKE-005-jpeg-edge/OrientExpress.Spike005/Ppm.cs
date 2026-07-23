using System.Globalization;
using System.Text;

namespace OrientExpress.Spike005;

/// <summary>
/// Minimal binary PPM (P6) raster: the lossless bridge between cjpeg/djpeg
/// and managed pixel math. The synthetic pattern is orientation-sensitive so
/// every rotation error is visible in a byte comparison.
/// </summary>
public sealed record Ppm(int Width, int Height, byte[] Pixels)
{
    public static Ppm CreateFixture(int width, int height)
    {
        var pixels = new byte[width * height * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 3;
                pixels[offset] = (byte)(x % 256);
                pixels[offset + 1] = (byte)(y % 256);
                pixels[offset + 2] = (byte)(((x * 7) + (y * 13)) % 256);
            }
        }

        return new Ppm(width, height, pixels);
    }

    public static Ppm Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var cursor = 0;
        string NextToken()
        {
            while (cursor < data.Length && char.IsWhiteSpace((char)data[cursor]))
            {
                cursor++;
            }

            if (cursor < data.Length && data[cursor] == (byte)'#')
            {
                while (cursor < data.Length && data[cursor] != (byte)'\n')
                {
                    cursor++;
                }

                return NextToken();
            }

            var start = cursor;
            while (cursor < data.Length && !char.IsWhiteSpace((char)data[cursor]))
            {
                cursor++;
            }

            return Encoding.ASCII.GetString(data, start, cursor - start);
        }

        if (NextToken() != "P6")
        {
            throw new InvalidDataException("not a binary PPM (P6)");
        }

        var width = int.Parse(NextToken(), CultureInfo.InvariantCulture);
        var height = int.Parse(NextToken(), CultureInfo.InvariantCulture);
        _ = NextToken();
        cursor++;
        var pixels = new byte[width * height * 3];
        Array.Copy(data, cursor, pixels, 0, pixels.Length);
        return new Ppm(width, height, pixels);
    }

    public byte[] Serialize()
    {
        var header = Encoding.ASCII.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"P6\n{Width} {Height}\n255\n"));
        var output = new byte[header.Length + Pixels.Length];
        header.CopyTo(output, 0);
        Pixels.CopyTo(output, header.Length);
        return output;
    }

    /// <summary>Reference spatial rotation: quarterTurns clockwise.</summary>
    public Ppm Rotate(int quarterTurns)
    {
        var result = this;
        for (var turn = 0; turn < ((quarterTurns % 4) + 4) % 4; turn++)
        {
            result = result.Rotate90Cw();
        }

        return result;
    }

    private Ppm Rotate90Cw()
    {
        var rotated = new byte[Pixels.Length];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var source = ((y * Width) + x) * 3;
                var destination = ((x * Height) + (Height - 1 - y)) * 3;
                rotated[destination] = Pixels[source];
                rotated[destination + 1] = Pixels[source + 1];
                rotated[destination + 2] = Pixels[source + 2];
            }
        }

        return new Ppm(Height, Width, rotated);
    }

    /// <summary>Largest absolute per-channel difference; 255 when dimensions differ.</summary>
    public static int MaxChannelDelta(Ppm expected, Ppm actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return 255;
        }

        var maxDelta = 0;
        for (var index = 0; index < expected.Pixels.Length; index++)
        {
            maxDelta = int.Max(maxDelta, int.Abs(expected.Pixels[index] - actual.Pixels[index]));
        }

        return maxDelta;
    }

    public static (long DiffBytes, long Total) Compare(Ppm expected, Ppm actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return (long.Max(expected.Pixels.Length, actual.Pixels.Length), expected.Pixels.Length);
        }

        long diff = 0;
        for (var index = 0; index < expected.Pixels.Length; index++)
        {
            if (expected.Pixels[index] != actual.Pixels[index])
            {
                diff++;
            }
        }

        return (diff, expected.Pixels.Length);
    }
}
