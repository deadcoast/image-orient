using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Orientation;

namespace OrientExpress.Application.Orientation;

/// <summary>
/// COMP-ORIENTATION (WP-007): the SPIKE-002 four-candidate approach —
/// present all four quarter-turn restorations of a photo to the face
/// detector, summarize the evidence per candidate, and let the policy
/// decide or abstain. One decode per photo; the four rotations share it.
/// </summary>
public sealed class FourCandidateOrientationEngine(
    IPixelDecoder decoder,
    IFaceDetector detector,
    string modelIdentity,
    string runtimeIdentity)
{
    /// <summary>Analysis long-edge bound: cheap rotations, detector resizes to its own input anyway.</summary>
    public const int AnalysisLongEdge = 960;

    public async ValueTask<Result<OrientationAssessment>> AssessAsync(
        NormalizedPath photo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(photo);
        var decoded = decoder.Decode(photo, AnalysisLongEdge);
        if (!decoded.IsSuccess)
        {
            return Result.Fail<OrientationAssessment>(decoded.Error!);
        }

        var candidates = new CandidateSummary[4];
        for (var quarterTurns = 0; quarterTurns < 4; quarterTurns++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rotated = PixelRasterOperations.Rotate(decoded.Require(), quarterTurns);
            var detected = await detector.DetectAsync(
                rotated, FourCandidatePolicy.DetectionThreshold, cancellationToken).ConfigureAwait(false);
            if (!detected.IsSuccess)
            {
                return Result.Fail<OrientationAssessment>(detected.Error!);
            }

            var faces = detected.Require();
            candidates[quarterTurns] = new CandidateSummary(
                quarterTurns,
                faces.Count > 0 ? faces[0].Score : 0,
                faces.Where(face => face.Score >= FourCandidatePolicy.VoteScoreThreshold).Sum(face => face.Score),
                faces.Count,
                faces.Count > 0 && faces[0].EyesAboveMouth);
        }

        return Result.Ok(FourCandidatePolicy.Decide(candidates, modelIdentity, runtimeIdentity));
    }
}
