using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrientExpress.Spike009.Simulation;

public sealed record SealedPlan(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("contractVersion")] string ContractVersion,
    [property: JsonPropertyName("jobId")] string JobId,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("rows")] IReadOnlyList<PlanRow> Rows,
    [property: JsonPropertyName("rowsSha256")] string RowsSha256);

/// <summary>
/// Minimal sealed-plan store proving the scan-seals/apply-consumes contract
/// (ADR-0003): apply revalidates the row digest and refuses stale plans.
/// </summary>
public static class PlanStore
{
    public const string PlanFileName = "plan.json";

    private static readonly JsonSerializerOptions FileOptions = new() { WriteIndented = true };

    public static SealedPlan Seal(string jobDirectory, string contractVersion, string mode, IReadOnlyList<PlanRow> rows)
    {
        Directory.CreateDirectory(jobDirectory);
        var digest = RowsDigest(rows);
        var plan = new SealedPlan(
            SchemaVersion: "1.0",
            ContractVersion: contractVersion,
            JobId: "job-" + digest[..12],
            Mode: mode,
            Rows: rows,
            RowsSha256: digest);
        File.WriteAllText(
            Path.Combine(jobDirectory, PlanFileName),
            JsonSerializer.Serialize(plan, FileOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return plan;
    }

    public static bool TryLoad(string jobDirectory, out SealedPlan? plan, out bool digestValid)
    {
        plan = null;
        digestValid = false;
        var path = Path.Combine(jobDirectory, PlanFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        plan = JsonSerializer.Deserialize<SealedPlan>(File.ReadAllText(path));
        if (plan is null)
        {
            return false;
        }

        digestValid = RowsDigest(plan.Rows) == plan.RowsSha256;
        return true;
    }

    public static string RowsDigest(IReadOnlyList<PlanRow> rows)
        => Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows, EventJson.Canonical))));

    private static class EventJson
    {
        public static readonly JsonSerializerOptions Canonical = new() { WriteIndented = false };
    }
}
