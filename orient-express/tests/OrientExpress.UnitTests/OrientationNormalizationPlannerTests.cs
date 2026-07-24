using OrientExpress.Application.Transform;
using OrientExpress.Contracts.Transform;

namespace OrientExpress.UnitTests;

/// <summary>
/// AC-006 / PRD-FR-012: the EXIF display transform is deterministic. All
/// eight orientation values map to exactly one lossless operation, the
/// transpose family swaps expected dimensions, and every plan sets the tag
/// to 1.
/// </summary>
public sealed class OrientationNormalizationPlannerTests
{
    [Theory]
    [InlineData(1, JpegOperation.None, false)]
    [InlineData(2, JpegOperation.FlipHorizontal, false)]
    [InlineData(3, JpegOperation.Rotate180, false)]
    [InlineData(4, JpegOperation.FlipVertical, false)]
    [InlineData(5, JpegOperation.Transpose, true)]
    [InlineData(6, JpegOperation.Rotate90, true)]
    [InlineData(7, JpegOperation.Transverse, true)]
    [InlineData(8, JpegOperation.Rotate270, true)]
    public void All_eight_exif_values_map_to_one_lossless_operation(
        int exifOrientation, JpegOperation expectedOperation, bool swapsDimensions)
    {
        var plan = OrientationNormalizationPlanner
            .FromExifOrientation(exifOrientation, 640, 480, JpegEdgePolicy.Abort)
            .Require();

        Assert.Equal(expectedOperation, plan.Operation);
        Assert.Equal(1, plan.SetOrientationTo);
        Assert.Equal(swapsDimensions ? 480 : 640, plan.ExpectedWidth);
        Assert.Equal(swapsDimensions ? 640 : 480, plan.ExpectedHeight);
        Assert.Equal(JpegEdgePolicy.Abort, plan.EdgePolicy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(-1)]
    public void Out_of_range_orientation_is_a_typed_error(int exifOrientation)
    {
        var result = OrientationNormalizationPlanner.FromExifOrientation(
            exifOrientation, 100, 100, JpegEdgePolicy.Abort);
        Assert.False(result.IsSuccess);
        Assert.Equal("ORIENTATION_VALUE_INVALID", result.Error!.Code);
    }

    [Theory]
    [InlineData("abort", JpegEdgePolicy.Abort)]
    [InlineData("preserve-partial", JpegEdgePolicy.PreservePartial)]
    [InlineData("trim", JpegEdgePolicy.Trim)]
    [InlineData("reencode", JpegEdgePolicy.Reencode)]
    public void Configured_edge_policy_values_parse(string configured, JpegEdgePolicy expected)
        => Assert.Equal(expected, OrientationNormalizationPlanner.ParseEdgePolicy(configured).Require());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ABORT")]
    public void Unknown_edge_policy_values_are_typed_errors(string? configured)
        => Assert.Equal(
            "CONFIG_JPEG_EDGE_INVALID",
            OrientationNormalizationPlanner.ParseEdgePolicy(configured).Error!.Code);
}
