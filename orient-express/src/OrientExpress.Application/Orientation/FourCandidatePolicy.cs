using OrientExpress.Contracts.Orientation;

namespace OrientExpress.Application.Orientation;

/// <summary>
/// COMP-POLICY (WP-008), provisional and UNCALIBRATED: the conservative
/// vote/geometry/margin rule measured on the operator's first corpus run.
/// Decides only when every signal agrees; everything else is Review —
/// abstention-until-calibrated is the recorded ADR-0005 deviation, and the
/// policy identity says so. Calibrated thresholds replace these constants
/// when the SPIKE-001/002 corpus evidence exists (WP-020).
/// </summary>
public static class FourCandidatePolicy
{
    public const string Identity = "four-candidate-vote-geometry-margin/provisional-uncalibrated";

    /// <summary>Only clearly detected faces vote (weak spurious detections must not out-vote a real face).</summary>
    public const float VoteScoreThreshold = 0.6f;

    /// <summary>Detection floor for building candidate evidence.</summary>
    public const float DetectionThreshold = 0.3f;

    /// <summary>Winner must beat the runner-up by this fraction of its own vote.</summary>
    public const float RelativeMargin = 0.08f;

    public static OrientationAssessment Decide(
        IReadOnlyList<CandidateSummary> candidates, string modelIdentity, string runtimeIdentity)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfNotEqual(candidates.Count, 4);

        var chosen = 0;
        for (var index = 1; index < 4; index++)
        {
            if (candidates[index].VoteScore > candidates[chosen].VoteScore)
            {
                chosen = index;
            }
        }

        var runnerUp = float.MinValue;
        for (var index = 0; index < 4; index++)
        {
            if (index != chosen && candidates[index].VoteScore > runnerUp)
            {
                runnerUp = candidates[index].VoteScore;
            }
        }

        var margin = candidates[chosen].VoteScore - runnerUp;
        var relative = candidates[chosen].VoteScore > 0 ? margin / candidates[chosen].VoteScore : 0f;

        var reasons = new List<string>();
        if (candidates.All(candidate => candidate.FaceCount == 0))
        {
            reasons.Add("ML_NO_FACE");
        }
        else
        {
            if (candidates[chosen].VoteScore <= 0)
            {
                reasons.Add("ML_NO_CONFIDENT_FACE");
            }

            if (!candidates[chosen].UprightGeometry)
            {
                reasons.Add("ML_GEOMETRY_DISAGREES");
            }

            if (relative < RelativeMargin)
            {
                reasons.Add("ML_MARGIN_TOO_SMALL");
            }
        }

        var decision = reasons.Count > 0
            ? OrientationDecision.Review
            : chosen switch
            {
                0 => OrientationDecision.Upright,
                1 => OrientationDecision.Rotate90Cw,
                2 => OrientationDecision.Rotate180,
                _ => OrientationDecision.Rotate270Cw,
            };
        return new OrientationAssessment(
            candidates,
            decision,
            margin,
            relative,
            reasons,
            modelIdentity,
            runtimeIdentity,
            Identity,
            Calibrated: false);
    }
}
