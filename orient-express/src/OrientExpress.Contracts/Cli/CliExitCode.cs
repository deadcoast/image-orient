namespace OrientExpress.Contracts.Cli;

/// <summary>
/// The DOC-CLI exit-code registry (PRD-FR-018). Numerics are FROZEN as of
/// WP-002 entry (2026-07-23, cli-contract/1.0): values may be added within
/// their bands but never repurposed within this contract major version.
/// Symbols from <see cref="CliExitCodeRegistry"/> are the stable automation
/// vocabulary.
/// </summary>
public enum CliExitCode
{
    Success = 0,
    ReviewRequired = 10,
    PartialCompletion = 11,
    NothingToDo = 12,
    InvalidArgument = 20,
    InvalidConfiguration = 21,
    CapabilityUnavailable = 22,
    IncompatibleContract = 23,
    StalePlan = 30,
    SafetyPreflightFailed = 31,
    VerificationFailed = 32,
    RecoveryRequired = 33,
    Conflict = 34,
    Cancelled = 40,
    InternalError = 50,
}

public static class CliExitCodeRegistry
{
    public static string SymbolOf(CliExitCode code)
        => code switch
        {
            CliExitCode.Success => "SUCCESS",
            CliExitCode.ReviewRequired => "REVIEW_REQUIRED",
            CliExitCode.PartialCompletion => "PARTIAL_COMPLETION",
            CliExitCode.NothingToDo => "NOTHING_TO_DO",
            CliExitCode.InvalidArgument => "INVALID_ARGUMENT",
            CliExitCode.InvalidConfiguration => "INVALID_CONFIGURATION",
            CliExitCode.CapabilityUnavailable => "CAPABILITY_UNAVAILABLE",
            CliExitCode.IncompatibleContract => "INCOMPATIBLE_CONTRACT",
            CliExitCode.StalePlan => "STALE_PLAN",
            CliExitCode.SafetyPreflightFailed => "SAFETY_PREFLIGHT_FAILED",
            CliExitCode.VerificationFailed => "VERIFICATION_FAILED",
            CliExitCode.RecoveryRequired => "RECOVERY_REQUIRED",
            CliExitCode.Conflict => "CONFLICT",
            CliExitCode.Cancelled => "CANCELLED",
            CliExitCode.InternalError => "INTERNAL_ERROR",
            _ => "UNKNOWN",
        };
}
