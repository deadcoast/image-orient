using OrientExpress.Application.Orientation;
using OrientExpress.Contracts.Orientation;

namespace OrientExpress.UnitTests;

/// <summary>
/// PRD-FR-006 decision matrix for the provisional uncalibrated policy:
/// rotate ONLY when the vote, the winner's landmark geometry, and the
/// relative margin all agree; everything else is Review with named
/// reasons. Every assessment carries provenance and Calibrated=false.
/// </summary>
public sealed class FourCandidatePolicyTests
{
    private static CandidateSummary Candidate(
        int turns, float vote, bool geometry = true, int faces = 1, float top = 0.9f)
        => new(turns, top, vote, faces, geometry);

    private static OrientationAssessment Decide(params CandidateSummary[] candidates)
        => FourCandidatePolicy.Decide(candidates, "model-x", "runtime-y");

    [Fact]
    public void Confident_winner_with_geometry_and_margin_rotates()
    {
        var assessment = Decide(
            Candidate(0, 0.2f, geometry: false),
            Candidate(1, 0.95f),
            Candidate(2, 0.3f, geometry: false),
            Candidate(3, 0.1f, geometry: false));

        Assert.Equal(OrientationDecision.Rotate90Cw, assessment.Decision);
        Assert.Empty(assessment.ReasonCodes);
        Assert.False(assessment.Calibrated);
        Assert.Equal("model-x", assessment.ModelIdentity);
        Assert.Equal(FourCandidatePolicy.Identity, assessment.PolicyIdentity);
    }

    [Fact]
    public void Confident_upright_stays_upright()
        => Assert.Equal(
            OrientationDecision.Upright,
            Decide(Candidate(0, 0.9f), Candidate(1, 0.2f), Candidate(2, 0.1f), Candidate(3, 0.1f)).Decision);

    [Fact]
    public void Geometry_disagreement_parks_for_review()
    {
        var assessment = Decide(
            Candidate(0, 0.2f),
            Candidate(1, 0.95f, geometry: false),
            Candidate(2, 0.3f),
            Candidate(3, 0.1f));
        Assert.Equal(OrientationDecision.Review, assessment.Decision);
        Assert.Contains("ML_GEOMETRY_DISAGREES", assessment.ReasonCodes);
    }

    [Fact]
    public void Thin_margin_parks_for_review()
    {
        var assessment = Decide(
            Candidate(0, 0.90f),
            Candidate(1, 0.88f),
            Candidate(2, 0.1f),
            Candidate(3, 0.1f));
        Assert.Equal(OrientationDecision.Review, assessment.Decision);
        Assert.Contains("ML_MARGIN_TOO_SMALL", assessment.ReasonCodes);
    }

    [Fact]
    public void No_faces_anywhere_parks_for_review_with_the_no_face_reason()
    {
        var assessment = Decide(
            Candidate(0, 0f, faces: 0, top: 0f),
            Candidate(1, 0f, faces: 0, top: 0f),
            Candidate(2, 0f, faces: 0, top: 0f),
            Candidate(3, 0f, faces: 0, top: 0f));
        Assert.Equal(OrientationDecision.Review, assessment.Decision);
        Assert.Equal(["ML_NO_FACE"], assessment.ReasonCodes);
    }

    [Fact]
    public void Weak_faces_only_park_for_review_not_rotate()
    {
        // Faces detected, but none clears the vote threshold: the sum is 0.
        var assessment = Decide(
            Candidate(0, 0f, faces: 2, top: 0.4f),
            Candidate(1, 0f, faces: 1, top: 0.5f),
            Candidate(2, 0f, faces: 0, top: 0f),
            Candidate(3, 0f, faces: 0, top: 0f));
        Assert.Equal(OrientationDecision.Review, assessment.Decision);
        Assert.Contains("ML_NO_CONFIDENT_FACE", assessment.ReasonCodes);
    }
}
