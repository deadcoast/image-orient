using System.Text.Json.Serialization;
using OrientExpress.Spike009.Contract;

namespace OrientExpress.Spike009.Simulation;

/// <summary>One planned/applied item. Property order is the canonical digest order.</summary>
public sealed record PlanRow(
    [property: JsonPropertyName("pathToken")] string PathToken,
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("reasonCode")] string? ReasonCode);

/// <summary>
/// Deterministic stand-in for the orientation planner: outcomes derive from
/// markers in the synthetic path token so contract behavior is reproducible
/// without touching real images.
/// </summary>
public static class SyntheticClassifier
{
    public static PlanRow Classify(string pathToken)
    {
        var token = pathToken.ToUpperInvariant();
        if (token.Contains("ROT90", StringComparison.Ordinal))
        {
            return new PlanRow(pathToken, "ROTATE_90_CW", "succeeded", null);
        }

        if (token.Contains("ROT180", StringComparison.Ordinal))
        {
            return new PlanRow(pathToken, "ROTATE_180", "succeeded", null);
        }

        if (token.Contains("ROT270", StringComparison.Ordinal))
        {
            return new PlanRow(pathToken, "ROTATE_270_CW", "succeeded", null);
        }

        if (token.Contains("REVIEW", StringComparison.Ordinal))
        {
            return new PlanRow(pathToken, "REVIEW", "review", "LOW_CONFIDENCE");
        }

        if (token.Contains("UNSUPPORTED", StringComparison.Ordinal))
        {
            return new PlanRow(pathToken, "UNSUPPORTED", "unsupported", "FORMAT_UNSUPPORTED");
        }

        if (token.Contains("ERROR", StringComparison.Ordinal))
        {
            return new PlanRow(pathToken, "ERROR", "failed", "SIMULATED_IO_ERROR");
        }

        return new PlanRow(pathToken, "KEEP", "skipped", "ALREADY_UPRIGHT");
    }

    /// <summary>
    /// Exit aggregation rule under qualification (documented in the report):
    /// item failures dominate, then review, then success, then capability,
    /// then nothing-to-do.
    /// </summary>
    public static CliExitCode Aggregate(IReadOnlyList<PlanRow> rows, bool cancelled)
    {
        if (cancelled)
        {
            return CliExitCode.Cancelled;
        }

        var failed = rows.Count(row => row.Result == "failed");
        var review = rows.Count(row => row.Result == "review");
        var succeeded = rows.Count(row => row.Result == "succeeded");
        var unsupported = rows.Count(row => row.Result == "unsupported");
        if (failed > 0)
        {
            return CliExitCode.PartialCompletion;
        }

        if (review > 0)
        {
            return CliExitCode.ReviewRequired;
        }

        if (succeeded > 0)
        {
            return CliExitCode.Success;
        }

        return unsupported > 0 ? CliExitCode.CapabilityUnavailable : CliExitCode.NothingToDo;
    }

    public static IReadOnlyDictionary<string, int> Counts(IReadOnlyList<PlanRow> rows)
        => new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["total"] = rows.Count,
            ["succeeded"] = rows.Count(row => row.Result == "succeeded"),
            ["review"] = rows.Count(row => row.Result == "review"),
            ["skipped"] = rows.Count(row => row.Result == "skipped"),
            ["unsupported"] = rows.Count(row => row.Result == "unsupported"),
            ["failed"] = rows.Count(row => row.Result == "failed"),
            ["cancelled"] = rows.Count(row => row.Result == "cancelled"),
        };
}
