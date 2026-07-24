using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace OrientExpress.Spike002;

/// <summary>One detected face: score, box, and the five YuNet landmarks.</summary>
public sealed record FaceDetection(
    float Score,
    float X,
    float Y,
    float W,
    float H,
    IReadOnlyList<(float X, float Y)> Landmarks)
{
    /// <summary>Landmark order: right eye, left eye, nose, right mouth, left mouth.</summary>
    public bool EyesAboveMouth
        => ((Landmarks[0].Y + Landmarks[1].Y) / 2f) < ((Landmarks[3].Y + Landmarks[4].Y) / 2f);
}

/// <summary>
/// YuNet (face_detection_yunet_2023mar) on ONNX Runtime CPU: fixed square
/// input, BGR 0–255, twelve raw output heads (cls/obj/bbox/kps at strides
/// 8/16/32) decoded per the OpenCV FaceDetectorYN post-processing, then NMS.
/// </summary>
public sealed class YuNetDetector : IDisposable
{
    public const int InputSize = 640;
    private static readonly int[] Strides = [8, 16, 32];

    private readonly InferenceSession _session;
    private readonly string _inputName;

    public YuNetDetector(string modelPath, SessionOptions? options = null)
    {
        _session = options is null ? new InferenceSession(modelPath) : new InferenceSession(modelPath, options);
        _inputName = _session.InputMetadata.Keys.Single();
    }

    public string InputName => _inputName;

    public IReadOnlyList<FaceDetection> Detect(PixelImage image, float scoreThreshold = 0.6f)
    {
        ArgumentNullException.ThrowIfNull(image);
        var resized = image.ResizeBilinear(InputSize, InputSize);
        var raw = RunRaw(resized.ToBgrChwTensor());
        var faces = Decode(raw, scoreThreshold);
        var kept = NonMaxSuppression(faces, 0.3f);

        // Map from model space back to source-image coordinates.
        var scaleX = (float)image.Width / InputSize;
        var scaleY = (float)image.Height / InputSize;
        return [.. kept.Select(face => face with
        {
            X = face.X * scaleX,
            Y = face.Y * scaleY,
            W = face.W * scaleX,
            H = face.H * scaleY,
            Landmarks = [.. face.Landmarks.Select(point => (point.X * scaleX, point.Y * scaleY))],
        })];
    }

    /// <summary>Raw forward pass for determinism/latency evidence (SPIKE-003).</summary>
    public IReadOnlyDictionary<string, float[]> RunRaw(float[] bgrChwTensor)
    {
        ArgumentNullException.ThrowIfNull(bgrChwTensor);
        var input = new DenseTensor<float>(bgrChwTensor, [1, 3, InputSize, InputSize]);
        using var results = _session.Run([NamedOnnxValue.CreateFromTensor(_inputName, input)]);
        return results.ToDictionary(
            value => value.Name,
            value => value.AsEnumerable<float>().ToArray(),
            StringComparer.Ordinal);
    }

    private static List<FaceDetection> Decode(IReadOnlyDictionary<string, float[]> outputs, float scoreThreshold)
    {
        var faces = new List<FaceDetection>();
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

                faces.Add(new FaceDetection(score, cx - (w / 2), cy - (h / 2), w, h, landmarks));
            }
        }

        return faces;
    }

    public static List<FaceDetection> NonMaxSuppression(List<FaceDetection> faces, float iouThreshold)
    {
        var kept = new List<FaceDetection>();
        foreach (var candidate in faces.OrderByDescending(face => face.Score))
        {
            if (kept.All(existing => IntersectionOverUnion(existing, candidate) < iouThreshold))
            {
                kept.Add(candidate);
            }
        }

        return kept;
    }

    public static float IntersectionOverUnion(FaceDetection a, FaceDetection b)
    {
        var x1 = MathF.Max(a.X, b.X);
        var y1 = MathF.Max(a.Y, b.Y);
        var x2 = MathF.Min(a.X + a.W, b.X + b.W);
        var y2 = MathF.Min(a.Y + a.H, b.Y + b.H);
        var intersection = MathF.Max(0, x2 - x1) * MathF.Max(0, y2 - y1);
        var union = (a.W * a.H) + (b.W * b.H) - intersection;
        return union <= 0 ? 0 : intersection / union;
    }

    public void Dispose() => _session.Dispose();
}
