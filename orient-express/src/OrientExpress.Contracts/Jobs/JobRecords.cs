using System.Text.Json.Serialization;
using OrientExpress.Contracts.Transform;

namespace OrientExpress.Contracts.Jobs;

/// <summary>Job identity: time-ordered, filesystem-safe, unique per job root.</summary>
public sealed record JobId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>
/// DOC-DATAMODEL `manifest.json`: the immutable identity/config summary
/// written at creation and never rewritten. Model identity is null until an
/// ML assessment actually participates (the deterministic EXIF path has no
/// model) — absence is honest, not a placeholder.
/// </summary>
public sealed record JobManifest
{
    [JsonPropertyName("jobId")]
    public required string JobIdValue { get; init; }

    [JsonPropertyName("contractVersion")]
    public string ContractVersion { get; init; } = "job-store/1.0";

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("createdUtc")]
    public required string CreatedUtc { get; init; }

    [JsonPropertyName("toolVersion")]
    public required string ToolVersion { get; init; }

    /// <summary>`safe-copy` or `in-place` (in-place stays gated until WP-015).</summary>
    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("sourceRoot")]
    public required string SourceRoot { get; init; }

    /// <summary>Safe-copy destination root; outputs mirror the source-relative layout beneath it.</summary>
    [JsonPropertyName("outputRoot")]
    public required string OutputRoot { get; init; }

    [JsonPropertyName("configDigest")]
    public required string ConfigDigest { get; init; }

    [JsonPropertyName("modelIdentity")]
    public string? ModelIdentity { get; init; }

    [JsonPropertyName("policyIdentity")]
    public string? PolicyIdentity { get; init; }
}

public enum JobState
{
    Created,
    Scanning,
    Sealed,
    Applying,
    Completed,
    Failed,
}

public sealed record JobHandle(JobId Id, JobManifest Manifest, string JobDirectory);

/// <summary>The plan-level decision for one file (DOC-DATAMODEL `plans.effective decision`).</summary>
public enum PlanAction
{
    /// <summary>Already upright; nothing to do.</summary>
    Keep,

    /// <summary>Physically apply <see cref="PlannedFile.Operation"/> and set the orientation tag to 1.</summary>
    Normalize,

    /// <summary>Uncertain; parked for the review workflow, never rotated silently.</summary>
    Review,

    /// <summary>Outside current capability (format, codec, filesystem class); reason recorded.</summary>
    Unsupported,
}

/// <summary>
/// One scanned file plus its planned outcome — the row that is sealed and
/// later consumed by apply (PRD-FR-007). Identity fields are recorded so
/// apply can refuse stale inputs (PRD-FR-008); absolute paths stay local to
/// the job database (DOC-DATAMODEL).
/// </summary>
public sealed record PlannedFile
{
    public required long Sequence { get; init; }

    public required string SourcePath { get; init; }

    public required long SizeBytes { get; init; }

    public required string LastWriteUtc { get; init; }

    public required string PrefixSha256 { get; init; }

    public string? FullSha256 { get; init; }

    public required string VolumeSerialHex { get; init; }

    public required string FileIdHex { get; init; }

    public required string Format { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    /// <summary>Stored EXIF orientation 1..8; 0 = absent.</summary>
    public int ExifOrientation { get; init; }

    public required PlanAction Action { get; init; }

    public JpegOperation Operation { get; init; }

    public JpegEdgePolicy EdgePolicy { get; init; }

    public required string OutputToken { get; init; }

    public int ExpectedWidth { get; init; }

    public int ExpectedHeight { get; init; }

    public int ExpectedOrientation { get; init; }

    public string? ReasonCode { get; init; }
}

/// <summary>The content-addressed seal over the ordered plan (AC-012/AC-013).</summary>
public sealed record PlanSeal(string Sha256, long RowCount, string SealedUtc);

/// <summary>Filter for streaming plan rows in canonical (sequence) order.</summary>
public sealed record PlanQuery(PlanAction? Action = null, long AfterSequence = 0, int Limit = int.MaxValue);

/// <summary>Append-only fact (DOC-DATAMODEL `events`); the store assigns the job sequence and time.</summary>
public sealed record JobEvent(
    string EventType,
    string Result,
    long? FileSequence = null,
    string? ReasonCode = null,
    string? DataJson = null);

/// <summary>
/// DOC-FSSAFE journal checkpoints — strictly forward-only (enforced by a
/// schema trigger; an unknown checkpoint name can never be written).
/// </summary>
public static class JournalCheckpoints
{
    public const string Prepared = "Prepared";
    public const string StagedVerified = "StagedVerified";
    public const string IntentDurable = "IntentDurable";
    public const string Replaced = "Replaced";
    public const string Committed = "Committed";
    public const string Reconciled = "Reconciled";
}

/// <summary>One durable intent row (DOC-DATAMODEL `journal`).</summary>
public sealed record JournalIntent(
    string IntentId,
    long FileSequence,
    string Operation,
    string Checkpoint,
    string? DetailsJson,
    string UpdatedUtc);

public sealed record IntegrityReport(
    bool DatabaseOk,
    bool SealPresent,
    bool SealMatches,
    long FileCount,
    long EventCount,
    long UnresolvedJournalIntents,
    IReadOnlyList<string> Problems);
