using OrientExpress.Spike002;

namespace OrientExpress.Spike002.Tests;

/// <summary>
/// CI-safe tests for the model-independent math (no ONNX model or network on
/// CI runners; the evidence run is operator-driven).
/// </summary>
public sealed class DetectorMathTests
{
    private static FaceDetection Face(float score, float x, float y, float w, float h)
        => new(score, x, y, w, h, [(0, 0), (10, 0), (5, 5), (2, 10), (8, 10)]);

    [Fact]
    public void Iou_is_one_for_identical_and_zero_for_disjoint_boxes()
    {
        var box = Face(0.9f, 10, 10, 20, 20);
        Assert.Equal(1f, YuNetDetector.IntersectionOverUnion(box, box), 3);
        Assert.Equal(0f, YuNetDetector.IntersectionOverUnion(box, Face(0.8f, 100, 100, 5, 5)));
    }

    [Fact]
    public void Nms_keeps_the_highest_scoring_of_overlapping_boxes()
    {
        var strong = Face(0.95f, 10, 10, 20, 20);
        var weakOverlap = Face(0.70f, 12, 12, 20, 20);
        var separate = Face(0.80f, 200, 200, 20, 20);

        var kept = YuNetDetector.NonMaxSuppression([weakOverlap, strong, separate], 0.3f);
        Assert.Equal(2, kept.Count);
        Assert.Contains(strong, kept);
        Assert.Contains(separate, kept);
    }

    [Fact]
    public void Eyes_above_mouth_flips_when_landmarks_invert()
    {
        var upright = Face(0.9f, 0, 0, 10, 15);
        Assert.True(upright.EyesAboveMouth);

        var inverted = upright with
        {
            Landmarks = [(0, 10), (10, 10), (5, 5), (2, 0), (8, 0)],
        };
        Assert.False(inverted.EyesAboveMouth);
    }

    [Fact]
    public void Pixel_rotation_is_an_involution_and_swaps_dimensions()
    {
        var pixels = new byte[4 * 2 * 3];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)index;
        }

        var image = new PixelImage(4, 2, pixels);
        var rotated = image.Rotate(1);
        Assert.Equal((2, 4), (rotated.Width, rotated.Height));
        Assert.Equal(image.Rgb, image.Rotate(4).Rgb);
    }

    [Fact]
    public void Bgr_tensor_is_planar_and_channel_swapped()
    {
        var image = new PixelImage(1, 1, [10, 20, 30]);
        Assert.Equal(new float[] { 30, 20, 10 }, image.ToBgrChwTensor());
    }

    [Fact]
    public void Bilinear_resize_preserves_uniform_color()
    {
        var pixels = new byte[8 * 8 * 3];
        Array.Fill(pixels, (byte)77);
        var resized = new PixelImage(8, 8, pixels).ResizeBilinear(3, 5);
        Assert.All(resized.Rgb, value => Assert.Equal(77, value));
    }
}
