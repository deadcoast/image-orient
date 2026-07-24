using OrientExpress.Contracts.Filesystem;

namespace OrientExpress.Contracts.Transform;

/// <summary>
/// The eight lossless DCT-domain operations jpegtran offers (SPIKE-005;
/// ADR-0007). Each EXIF orientation value 1..8 maps to exactly one of these
/// to bring stored pixels upright.
/// </summary>
public enum JpegOperation
{
    None,
    Rotate90,
    Rotate180,
    Rotate270,
    FlipHorizontal,
    FlipVertical,
    Transpose,
    Transverse,
}

/// <summary>
/// PRD-FR-011 policy for JPEGs whose dimensions are not MCU-aligned
/// (DOC-CONFIG `transform.jpegEdge`; SPIKE-005 mapping is normative).
/// </summary>
public enum JpegEdgePolicy
{
    /// <summary>`jpegtran -perfect`: refuse with JPEG_EDGE_NOT_PERFECT rather than touch edge blocks.</summary>
    Abort,

    /// <summary>jpegtran default: reversible transform with imperfect edge blocks, disclosed in the artifact.</summary>
    PreservePartial,

    /// <summary>`jpegtran -trim`: drop partial edge blocks — a visible dimension change.</summary>
    Trim,

    /// <summary>Decode and re-encode through the raster path (gated until that path exists).</summary>
    Reencode,
}

/// <summary>
/// DOC-COMPONENTS: created by policy, executed verbatim — the transformer
/// can never choose a fallback silently. `SetOrientationTo` implements
/// PRD-FR-012 (1 after physical normalization; 0 = leave metadata alone).
/// Expected dimensions are what WP-013 verification will compare against.
/// </summary>
public sealed record TransformPlan(
    JpegOperation Operation,
    JpegEdgePolicy EdgePolicy,
    int SetOrientationTo,
    int ExpectedWidth,
    int ExpectedHeight);

/// <summary>The exclusive same-volume stage location (DOC-FSSAFE) the transformer writes to.</summary>
public sealed record StagingTarget(NormalizedPath Path);

/// <summary>
/// What the transformer actually did. `CoefficientLossless` is true only
/// when the DCT path ran with `-perfect` semantics (or no coefficients were
/// touched at all); losslessness is otherwise a verification question
/// (WP-013), never an assumption.
/// </summary>
public sealed record TransformArtifact(
    NormalizedPath StagePath,
    JpegOperation AppliedOperation,
    JpegEdgePolicy AppliedEdgePolicy,
    bool CoefficientLossless,
    bool OrientationTagWritten);
