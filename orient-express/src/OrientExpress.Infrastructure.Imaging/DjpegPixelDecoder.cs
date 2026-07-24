using System.Diagnostics;
using System.Globalization;
using System.Text;
using OrientExpress.Application.Orientation;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Orientation;

namespace OrientExpress.Infrastructure.Imaging;

/// <summary>
/// Bounded analysis decode through the qualified libjpeg-turbo djpeg
/// (DEP-008): JPEG → binary PPM → RGB raster, downscaled to the analysis
/// bound. Read-only; never part of the transform path.
/// </summary>
public sealed class DjpegPixelDecoder(string djpegPath) : IPixelDecoder
{
    public Result<PixelRaster> Decode(NormalizedPath path, int maxLongEdge)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(djpegPath))
        {
            return Result.Fail<PixelRaster>(
                "DJPEG_NOT_FOUND", ErrorCategory.CapabilityUnavailable, false,
                "djpeg was not found; install libjpeg-turbo (DEP-008)");
        }

        var ppmPath = Path.Combine(Path.GetTempPath(), "upright-" + Guid.NewGuid().ToString("N") + ".ppm");
        try
        {
            var run = RunTool(djpegPath, ["-outfile", ppmPath, path.Display]);
            if (run.ExitCode != 0)
            {
                return Result.Fail<PixelRaster>(
                    "PIXEL_DECODE_FAILED", ErrorCategory.InvalidInput, false,
                    $"djpeg exit {run.ExitCode}: {run.Stderr.Split('\n', 2)[0].Trim()}");
            }

            var raster = ParsePpm(File.ReadAllBytes(ppmPath));
            var longEdge = Math.Max(raster.Width, raster.Height);
            if (longEdge > maxLongEdge)
            {
                var scale = (double)maxLongEdge / longEdge;
                raster = PixelRasterOperations.ResizeBilinear(
                    raster,
                    Math.Max(64, (int)(raster.Width * scale)),
                    Math.Max(64, (int)(raster.Height * scale)));
            }

            return Result.Ok(raster);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException)
        {
            return Result.Fail<PixelRaster>(
                "PIXEL_DECODE_FAILED", ErrorCategory.InvalidInput, false, exception.Message);
        }
        finally
        {
            if (File.Exists(ppmPath))
            {
                File.Delete(ppmPath);
            }
        }
    }

    private static PixelRaster ParsePpm(byte[] data)
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
        return new PixelRaster(width, height, pixels);
    }

    private static (int ExitCode, string Stderr) RunTool(string toolPath, IReadOnlyList<string> arguments)
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
        _ = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"{toolPath} timed out");
        }

        return (process.ExitCode, stderr);
    }
}
