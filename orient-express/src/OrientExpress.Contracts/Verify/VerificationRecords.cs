namespace OrientExpress.Contracts.Verify;

/// <summary>
/// What a staged output must prove before commit (PRD-FR-013): the plan
/// authored these expectations at scan time; verification compares the
/// reopened reality against them and nothing else.
/// </summary>
public sealed record VerificationContract(
    string ExpectedFormat,
    int ExpectedWidth,
    int ExpectedHeight,
    int ExpectedOrientation,
    long MinimumBytes = 4);

/// <summary>Stable verification failure codes (AC-025) — reasons, never booleans.</summary>
public static class VerificationFailureCodes
{
    public const string OutputMissing = "OUTPUT_MISSING";
    public const string OutputTooSmall = "OUTPUT_TOO_SMALL";
    public const string OutputDecodeFailed = "OUTPUT_DECODE_FAILED";
    public const string OutputWrongFormat = "OUTPUT_WRONG_FORMAT";
    public const string OutputWrongDimensions = "OUTPUT_WRONG_DIMENSIONS";
    public const string OutputWrongOrientation = "OUTPUT_WRONG_ORIENTATION";
}

public sealed record VerificationResult(bool Passed, IReadOnlyList<string> Failures)
{
    public static VerificationResult Pass { get; } = new(true, []);
}
