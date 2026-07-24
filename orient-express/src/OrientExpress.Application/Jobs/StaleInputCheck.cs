using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Jobs;

namespace OrientExpress.Application.Jobs;

/// <summary>
/// PRD-FR-008 / AC-014: apply consumes a sealed plan only when the file on
/// disk is still the file that was scanned. Pure comparison of the plan
/// row's recorded identity against a live snapshot; any drift is a named
/// reason, and the caller refuses — it never "probably fine"s its way past
/// a mismatch.
/// </summary>
public static class StaleInputCheck
{
    public static IReadOnlyList<string> FindDrift(PlannedFile planned, FileSnapshot live)
    {
        ArgumentNullException.ThrowIfNull(planned);
        ArgumentNullException.ThrowIfNull(live);
        var reasons = new List<string>();
        if (live.Fingerprint.SizeBytes != planned.SizeBytes)
        {
            reasons.Add("SIZE_CHANGED");
        }

        if (!string.Equals(live.Fingerprint.LastWriteUtc, planned.LastWriteUtc, StringComparison.Ordinal))
        {
            reasons.Add("LAST_WRITE_CHANGED");
        }

        if (!string.Equals(live.Fingerprint.PrefixSha256, planned.PrefixSha256, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("CONTENT_PREFIX_CHANGED");
        }

        if (planned.FullSha256 is not null
            && live.Fingerprint.FullSha256 is not null
            && !string.Equals(live.Fingerprint.FullSha256, planned.FullSha256, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("CONTENT_CHANGED");
        }

        if (!string.Equals(live.VolumeSerialHex, planned.VolumeSerialHex, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("VOLUME_CHANGED");
        }

        if (!string.Equals(live.FileIdHex, planned.FileIdHex, StringComparison.OrdinalIgnoreCase))
        {
            // SPIKE-006: the file id does not survive ReplaceFileW, so this
            // fires when someone replaced the file after scan — exactly the
            // case apply must refuse.
            reasons.Add("FILE_ID_CHANGED");
        }

        return reasons;
    }

    /// <summary>A plan row needing a stronger fingerprint than was captured cannot be satisfied (DOC-DATAMODEL).</summary>
    public static bool SatisfiesStrength(PlannedFile planned, FingerprintStrength required)
        => required != FingerprintStrength.Strong || planned.FullSha256 is not null;
}
