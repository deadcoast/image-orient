using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Transform;

namespace OrientExpress.Infrastructure.Imaging;

/// <summary>
/// Pure argument builder for jpegtran per the SPIKE-005 mapping approved in
/// the WBS: always `-copy all`; `abort` = `-perfect`; `preserve-partial` =
/// default mode; `trim` = `-trim`; `reencode` refuses (the raster re-encode
/// path does not exist yet, and a silent lossy substitute is forbidden).
/// </summary>
public static class JpegtranArguments
{
    public static Result<IReadOnlyList<string>> Build(TransformPlan plan, string sourcePath, string stagePath)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Operation == JpegOperation.None)
        {
            return Result.Fail<IReadOnlyList<string>>(
                "TRANSFORM_NOOP",
                ErrorCategory.InvalidInput,
                isRetryable: false,
                "Operation None does not invoke jpegtran; the transformer copies the source.");
        }

        if (plan.EdgePolicy == JpegEdgePolicy.Reencode)
        {
            return Result.Fail<IReadOnlyList<string>>(
                "TRANSFORM_REENCODE_UNAVAILABLE",
                ErrorCategory.CapabilityUnavailable,
                isRetryable: false,
                "The raster re-encode path is not implemented; choose abort, preserve-partial, or trim.");
        }

        var arguments = new List<string> { "-copy", "all" };
        arguments.AddRange(plan.Operation switch
        {
            JpegOperation.Rotate90 => ["-rotate", "90"],
            JpegOperation.Rotate180 => ["-rotate", "180"],
            JpegOperation.Rotate270 => ["-rotate", "270"],
            JpegOperation.FlipHorizontal => ["-flip", "horizontal"],
            JpegOperation.FlipVertical => ["-flip", "vertical"],
            JpegOperation.Transpose => ["-transpose"],
            _ => (string[])["-transverse"],
        });

        switch (plan.EdgePolicy)
        {
            case JpegEdgePolicy.Abort:
                arguments.Add("-perfect");
                break;
            case JpegEdgePolicy.Trim:
                arguments.Add("-trim");
                break;
            default:
                break;
        }

        arguments.AddRange(["-outfile", stagePath, sourcePath]);
        return Result.Ok<IReadOnlyList<string>>(arguments);
    }
}
