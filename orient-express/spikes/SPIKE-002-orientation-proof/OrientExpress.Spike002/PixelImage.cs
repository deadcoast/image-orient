using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OrientExpress.Spike002;

/// <summary>
/// Interleaved RGB raster with JPEG IO via the qualified libjpeg-turbo tools
/// (DEP-008), plus the exact-pixel operations the four-candidate approach
/// needs: quarter-turn rotation and bilinear resize.
/// </summary>
public sealed record PixelImage(int Width, int Height, byte[] Rgb)
{
    public static PixelImage LoadJpeg(string jpegPath, string toolsDirectory)
    {
        var ppmPath = Path.Combine(
            Path.GetTempPath(), "spike002-" + Guid.NewGuid().ToString("N") + ".ppm");
        try
        {
            RunTool(Path.Combine(toolsDirectory, "djpeg.exe"), ["-outfile", ppmPath, jpegPath]);
            return ParsePpm(File.ReadAllBytes(ppmPath));
        }
        finally
        {
            if (File.Exists(ppmPath))
            {
                File.Delete(ppmPath);
            }
        }
    }

    public PixelImage Rotate(int quarterTurnsClockwise)
    {
        var result = this;
        for (var turn = 0; turn < ((quarterTurnsClockwise % 4) + 4) % 4; turn++)
        {
            result = result.Rotate90Cw();
        }

        return result;
    }

    public PixelImage ResizeBilinear(int targetWidth, int targetHeight)
    {
        var output = new byte[targetWidth * targetHeight * 3];
        var scaleX = (double)Width / targetWidth;
        var scaleY = (double)Height / targetHeight;
        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = ((y + 0.5) * scaleY) - 0.5;
            var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, Height - 1);
            var y1 = Math.Min(y0 + 1, Height - 1);
            var fy = sourceY - y0;
            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = ((x + 0.5) * scaleX) - 0.5;
                var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, Width - 1);
                var x1 = Math.Min(x0 + 1, Width - 1);
                var fx = sourceX - x0;
                for (var channel = 0; channel < 3; channel++)
                {
                    double Sample(int sx, int sy) => Rgb[(((sy * Width) + sx) * 3) + channel];
                    var top = Sample(x0, y0) + ((Sample(x1, y0) - Sample(x0, y0)) * fx);
                    var bottom = Sample(x0, y1) + ((Sample(x1, y1) - Sample(x0, y1)) * fx);
                    output[(((y * targetWidth) + x) * 3) + channel] =
                        (byte)Math.Clamp(top + ((bottom - top) * fy), 0, 255);
                }
            }
        }

        return new PixelImage(targetWidth, targetHeight, output);
    }

    /// <summary>BGR float tensor in NCHW order, raw 0–255 values (YuNet preprocessing).</summary>
    public float[] ToBgrChwTensor()
    {
        var tensor = new float[3 * Width * Height];
        var planeSize = Width * Height;
        for (var index = 0; index < planeSize; index++)
        {
            tensor[index] = Rgb[(index * 3) + 2];
            tensor[planeSize + index] = Rgb[(index * 3) + 1];
            tensor[(2 * planeSize) + index] = Rgb[index * 3];
        }

        return tensor;
    }

    private PixelImage Rotate90Cw()
    {
        var rotated = new byte[Rgb.Length];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var source = ((y * Width) + x) * 3;
                var destination = ((x * Height) + (Height - 1 - y)) * 3;
                rotated[destination] = Rgb[source];
                rotated[destination + 1] = Rgb[source + 1];
                rotated[destination + 2] = Rgb[source + 2];
            }
        }

        return new PixelImage(Height, Width, rotated);
    }

    private static PixelImage ParsePpm(byte[] data)
    {
        var cursor = 0;
        string NextToken()
        {
            while (cursor < data.Length && char.IsWhiteSpace((char)data[cursor]))
            {
                cursor++;
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
            throw new InvalidDataException("djpeg output is not binary PPM");
        }

        var width = int.Parse(NextToken(), CultureInfo.InvariantCulture);
        var height = int.Parse(NextToken(), CultureInfo.InvariantCulture);
        _ = NextToken();
        cursor++;
        var pixels = new byte[width * height * 3];
        Array.Copy(data, cursor, pixels, 0, pixels.Length);
        return new PixelImage(width, height, pixels);
    }

    private static void RunTool(string toolPath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"failed to start {toolPath}");
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"{toolPath} timed out");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{toolPath} failed: {stderr.Trim()}");
        }
    }
}
