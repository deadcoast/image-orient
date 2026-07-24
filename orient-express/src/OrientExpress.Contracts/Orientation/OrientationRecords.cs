namespace OrientExpress.Contracts.Orientation;

/// <summary>Interleaved 8-bit RGB raster (COMP-CODEC decode surface).</summary>
public sealed record PixelRaster(int Width, int Height, byte[] Rgb);

/// <summary>One detected face: score, box, and the five YuNet landmarks in source coordinates.</summary>
public sealed record FaceObservation(
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

/// <summary>The evidence one restore candidate produced (WP-007 feature surface).</summary>
public sealed record CandidateSummary(
    int QuarterTurnsClockwise,
    float TopScore,
    float VoteScore,
    int FaceCount,
    bool UprightGeometry);

public enum OrientationDecision
{
    Upright,
    Rotate90Cw,
    Rotate180,
    Rotate270Cw,

    /// <summary>Not confident enough to act — parked for a human, never guessed (PRD-FR-006).</summary>
    Review,
}

/// <summary>
/// DOC-COMPONENTS `OrientationAssessment`: all four candidate summaries,
/// the selected decision with margins and reason codes, and full
/// model/runtime/policy provenance. `Calibrated` is FALSE until the
/// SPIKE-001/002 corpus evidence produces calibrated thresholds — an
/// uncalibrated assessment may rotate only under the conservative
/// provisional policy and must say so.
/// </summary>
public sealed record OrientationAssessment(
    IReadOnlyList<CandidateSummary> Candidates,
    OrientationDecision Decision,
    float VoteMargin,
    float RelativeMargin,
    IReadOnlyList<string> ReasonCodes,
    string ModelIdentity,
    string RuntimeIdentity,
    string PolicyIdentity,
    bool Calibrated);
