using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Transform;

namespace OrientExpress.Application.Transform;

/// <summary>
/// The deterministic (no-ML) normalization planner: maps a stored EXIF
/// orientation value 1..8 to the single lossless operation that makes the
/// stored pixels upright, after which the tag is set to 1 (PRD-FR-012,
/// AC-006 EXIF display transforms come first). Pure — safe to unit test
/// exhaustively.
/// </summary>
public static class OrientationNormalizationPlanner
{
    /// <summary>Orientation values whose display transform swaps width and height.</summary>
    private static bool SwapsDimensions(int exifOrientation) => exifOrientation >= 5;

    public static Result<TransformPlan> FromExifOrientation(
        int exifOrientation,
        int sourceWidth,
        int sourceHeight,
        JpegEdgePolicy edgePolicy)
    {
        if (exifOrientation is < 1 or > 8)
        {
            return Result.Fail<TransformPlan>(
                "ORIENTATION_VALUE_INVALID",
                ErrorCategory.InvalidInput,
                isRetryable: false,
                $"EXIF orientation must be 1..8; got {exifOrientation}.");
        }

        var operation = exifOrientation switch
        {
            1 => JpegOperation.None,
            2 => JpegOperation.FlipHorizontal,
            3 => JpegOperation.Rotate180,
            4 => JpegOperation.FlipVertical,
            5 => JpegOperation.Transpose,
            6 => JpegOperation.Rotate90,
            7 => JpegOperation.Transverse,
            _ => JpegOperation.Rotate270,
        };

        var swap = SwapsDimensions(exifOrientation);
        return Result.Ok(new TransformPlan(
            operation,
            edgePolicy,
            SetOrientationTo: 1,
            ExpectedWidth: swap ? sourceHeight : sourceWidth,
            ExpectedHeight: swap ? sourceWidth : sourceHeight));
    }

    /// <summary>Parses the validated DOC-CONFIG `output.jpegEdge` value.</summary>
    public static Result<JpegEdgePolicy> ParseEdgePolicy(string? configuredValue)
        => configuredValue switch
        {
            "abort" => Result.Ok(JpegEdgePolicy.Abort),
            "preserve-partial" => Result.Ok(JpegEdgePolicy.PreservePartial),
            "trim" => Result.Ok(JpegEdgePolicy.Trim),
            "reencode" => Result.Ok(JpegEdgePolicy.Reencode),
            _ => Result.Fail<JpegEdgePolicy>(
                "CONFIG_JPEG_EDGE_INVALID",
                ErrorCategory.InvalidInput,
                isRetryable: false,
                $"output.jpegEdge must be abort|preserve-partial|trim|reencode; got '{configuredValue}'."),
        };
}
