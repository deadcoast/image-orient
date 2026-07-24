using OrientExpress.Application.Inference;
using OrientExpress.Application.Orientation;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Models;
using OrientExpress.Contracts.Orientation;

namespace OrientExpress.Infrastructure.Inference;

/// <summary>
/// YuNet (face_detection_yunet_2023mar) behind the WP-006 runtime port:
/// fixed 640x640 BGR 0–255 input, twelve raw output heads (cls/obj/bbox/kps
/// at strides 8/16/32) decoded per OpenCV FaceDetectorYN, then NMS at IoU
/// 0.3. SPIKE-002/003-qualified math, productionized: the model artifact is
/// digest-verified at load by the runtime.
/// </summary>
public sealed class YuNetFaceDetector(IInferenceRuntime runtime, ModelHandle model) : IFaceDetector
{
    public const int InputSize = 640;
    private static readonly int[] Strides = [8, 16, 32];

    public async ValueTask<Result<IReadOnlyList<FaceObservation>>> DetectAsync(
        PixelRaster raster, float scoreThreshold, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(raster);
        var resized = PixelRasterOperations.ResizeBilinear(raster, InputSize, InputSize);
        var output = await runtime.RunAsync(
            model,
            new TensorInput("input", ToBgrChwTensor(resized), [1, 3, InputSize, InputSize]),
            InferenceOptions.Default,
            cancellationToken).ConfigureAwait(false);
        if (!output.IsSuccess)
        {
            return Result.Fail<IReadOnlyList<FaceObservation>>(output.Error!);
        }

        var faces = Decode(output.Require().Values, scoreThreshold);
        var kept = NonMaxSuppression(faces, 0.3f);
        var scaleX = (float)raster.Width / InputSize;
        var scaleY = (float)raster.Height / InputSize;
        return Result.Ok<IReadOnlyList<FaceObservation>>([.. kept.Select(face => face with
        {
            X = face.X * scaleX,
            Y = face.Y * scaleY,
            W = face.W * scaleX,
            H = face.H * scaleY,
            Landmarks = [.. face.Landmarks.Select(point => (point.X * scaleX, point.Y * scaleY))],
        })]);
    }

    private static float[] ToBgrChwTensor(PixelRaster raster)
    {
        var tensor = new float[3 * raster.Width * raster.Height];
        var planeSize = raster.Width * raster.Height;
        for (var index = 0; index < planeSize; index++)
        {
            tensor[index] = raster.Rgb[(index * 3) + 2];
            tensor[planeSize + index] = raster.Rgb[(index * 3) + 1];
            tensor[(2 * planeSize) + index] = raster.Rgb[index * 3];
        }

        return tensor;
    }

    private static List<FaceObservation> Decode(
        IReadOnlyDictionary<string, float[]> outputs, float scoreThreshold)
    {
        var faces = new List<FaceObservation>();
        foreach (var stride in Strides)
        {
            var cols = InputSize / stride;
            var rows = InputSize / stride;
            var cls = outputs[$"cls_{stride}"];
            var obj = outputs[$"obj_{stride}"];
            var bbox = outputs[$"bbox_{stride}"];
            var kps = outputs[$"kps_{stride}"];
            for (var cell = 0; cell < rows * cols; cell++)
            {
                var score = MathF.Sqrt(Math.Clamp(cls[cell], 0f, 1f) * Math.Clamp(obj[cell], 0f, 1f));
                if (score < scoreThreshold)
                {
                    continue;
                }

                var row = cell / cols;
                var col = cell % cols;
                var cx = (col + bbox[cell * 4]) * stride;
                var cy = (row + bbox[(cell * 4) + 1]) * stride;
                var w = MathF.Exp(bbox[(cell * 4) + 2]) * stride;
                var h = MathF.Exp(bbox[(cell * 4) + 3]) * stride;
                var landmarks = new (float, float)[5];
                for (var point = 0; point < 5; point++)
                {
                    landmarks[point] = (
                        (col + kps[(cell * 10) + (point * 2)]) * stride,
                        (row + kps[(cell * 10) + (point * 2) + 1]) * stride);
                }

                faces.Add(new FaceObservation(score, cx - (w / 2), cy - (h / 2), w, h, landmarks));
            }
        }

        return faces;
    }

    public static List<FaceObservation> NonMaxSuppression(List<FaceObservation> faces, float iouThreshold)
    {
        ArgumentNullException.ThrowIfNull(faces);
        var kept = new List<FaceObservation>();
        foreach (var candidate in faces.OrderByDescending(face => face.Score))
        {
            if (kept.All(existing => IntersectionOverUnion(existing, candidate) < iouThreshold))
            {
                kept.Add(candidate);
            }
        }

        return kept;
    }

    public static float IntersectionOverUnion(FaceObservation a, FaceObservation b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        var x1 = MathF.Max(a.X, b.X);
        var y1 = MathF.Max(a.Y, b.Y);
        var x2 = MathF.Min(a.X + a.W, b.X + b.W);
        var y2 = MathF.Min(a.Y + a.H, b.Y + b.H);
        var intersection = MathF.Max(0, x2 - x1) * MathF.Max(0, y2 - y1);
        var union = (a.W * a.H) + (b.W * b.H) - intersection;
        return union <= 0 ? 0 : intersection / union;
    }
}
