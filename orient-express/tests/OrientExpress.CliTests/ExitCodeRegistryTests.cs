using OrientExpress.Contracts.Cli;

namespace OrientExpress.CliTests;

/// <summary>
/// Freezes the cli-contract/1.0 registry numerics (PRD-FR-018, DOC-CLI).
/// A failure here means a breaking automation contract change.
/// </summary>
public sealed class ExitCodeRegistryTests
{
    [Theory]
    [InlineData(CliExitCode.Success, 0, "SUCCESS")]
    [InlineData(CliExitCode.ReviewRequired, 10, "REVIEW_REQUIRED")]
    [InlineData(CliExitCode.PartialCompletion, 11, "PARTIAL_COMPLETION")]
    [InlineData(CliExitCode.NothingToDo, 12, "NOTHING_TO_DO")]
    [InlineData(CliExitCode.InvalidArgument, 20, "INVALID_ARGUMENT")]
    [InlineData(CliExitCode.InvalidConfiguration, 21, "INVALID_CONFIGURATION")]
    [InlineData(CliExitCode.CapabilityUnavailable, 22, "CAPABILITY_UNAVAILABLE")]
    [InlineData(CliExitCode.IncompatibleContract, 23, "INCOMPATIBLE_CONTRACT")]
    [InlineData(CliExitCode.StalePlan, 30, "STALE_PLAN")]
    [InlineData(CliExitCode.SafetyPreflightFailed, 31, "SAFETY_PREFLIGHT_FAILED")]
    [InlineData(CliExitCode.VerificationFailed, 32, "VERIFICATION_FAILED")]
    [InlineData(CliExitCode.RecoveryRequired, 33, "RECOVERY_REQUIRED")]
    [InlineData(CliExitCode.Conflict, 34, "CONFLICT")]
    [InlineData(CliExitCode.Cancelled, 40, "CANCELLED")]
    [InlineData(CliExitCode.InternalError, 50, "INTERNAL_ERROR")]
    public void Registry_numerics_and_symbols_are_frozen(CliExitCode code, int numeric, string symbol)
    {
        Assert.Equal(numeric, (int)code);
        Assert.Equal(symbol, CliExitCodeRegistry.SymbolOf(code));
    }

    [Fact]
    public void Every_registry_value_has_a_distinct_symbol()
    {
        var symbols = Enum.GetValues<CliExitCode>().Select(CliExitCodeRegistry.SymbolOf).ToList();
        Assert.Equal(symbols.Count, symbols.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("UNKNOWN", symbols);
    }
}
