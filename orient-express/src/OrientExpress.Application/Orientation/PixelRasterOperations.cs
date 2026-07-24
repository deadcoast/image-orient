using OrientExpress.Contracts.Orientation;

namespace OrientExpress.Application.Orientation;

/// <summary>Exact-pixel raster operations the four-candidate approach needs (SPIKE-002 math, productionized).</summary>
public static class PixelRasterOperations
{
    public static PixelRaster Rotate(PixelRaster raster, int quarterTurnsClockwise)
    {
        ArgumentNullException.ThrowIfNull(raster);
        var result = raster;
        for (var turn = 0; turn < ((quarterTurnsClockwise % 4) + 4) % 4; turn++)
        {
            result = Rotate90Cw(result);
        }

        return result;
    }

    public static PixelRaster ResizeBilinear(PixelRaster raster, int targetWidth, int targetHeight)
    {
        ArgumentNullException.ThrowIfNull(raster);
        var output = new byte[targetWidth * targetHeight * 3];
        var scaleX = (double)raster.Width / targetWidth;
        var scaleY = (double)raster.Height / targetHeight;
        for (var y = 0; y < targetHeight; y++)
        {
            var sourceY = ((y + 0.5) * scaleY) - 0.5;
            var y0 = Math.Clamp((int)Math.Floor(sourceY), 0, raster.Height - 1);
            var y1 = Math.Min(y0 + 1, raster.Height - 1);
            var fy = sourceY - y0;
            for (var x = 0; x < targetWidth; x++)
            {
                var sourceX = ((x + 0.5) * scaleX) - 0.5;
                var x0 = Math.Clamp((int)Math.Floor(sourceX), 0, raster.Width - 1);
                var x1 = Math.Min(x0 + 1, raster.Width - 1);
                var fx = sourceX - x0;
                for (var channel = 0; channel < 3; channel++)
                {
                    double Sample(int sx, int sy) => raster.Rgb[(((sy * raster.Width) + sx) * 3) + channel];
                    var top = Sample(x0, y0) + ((Sample(x1, y0) - Sample(x0, y0)) * fx);
                    var bottom = Sample(x0, y1) + ((Sample(x1, y1) - Sample(x0, y1)) * fx);
                    output[(((y * targetWidth) + x) * 3) + channel] =
                        (byte)Math.Clamp(top + ((bottom - top) * fy), 0, 255);
                }
            }
        }

        return new PixelRaster(targetWidth, targetHeight, output);
    }

    private static PixelRaster Rotate90Cw(PixelRaster raster)
    {
        var rotated = new byte[raster.Rgb.Length];
        for (var y = 0; y < raster.Height; y++)
        {
            for (var x = 0; x < raster.Width; x++)
            {
                var source = ((y * raster.Width) + x) * 3;
                var destination = ((x * raster.Height) + (raster.Height - 1 - y)) * 3;
                rotated[destination] = raster.Rgb[source];
                rotated[destination + 1] = raster.Rgb[source + 1];
                rotated[destination + 2] = raster.Rgb[source + 2];
            }
        }

        return new PixelRaster(raster.Height, raster.Width, rotated);
    }
}
