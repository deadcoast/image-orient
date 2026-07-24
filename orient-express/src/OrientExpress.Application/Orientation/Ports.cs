using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Orientation;

namespace OrientExpress.Application.Orientation;

/// <summary>Bounded pixel decode for analysis (COMP-CODEC): read-only, downscaled, never the transform path.</summary>
public interface IPixelDecoder
{
    public Result<PixelRaster> Decode(NormalizedPath path, int maxLongEdge);
}

/// <summary>COMP-INFERENCE face detection over a raster (WP-006 runtime behind it).</summary>
public interface IFaceDetector
{
    public ValueTask<Result<IReadOnlyList<FaceObservation>>> DetectAsync(
        PixelRaster raster, float scoreThreshold, CancellationToken cancellationToken);
}
