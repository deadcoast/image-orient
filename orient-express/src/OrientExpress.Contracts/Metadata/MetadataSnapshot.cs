namespace OrientExpress.Contracts.Metadata;

/// <summary>
/// Field-aware metadata state (NFR-FID-001): group-qualified tag names
/// (`IFD0:`, `ExifIFD:`, `GPS:`, `XMP-*:`, `IPTC:`) mapped to normalized
/// string values, as read by the metadata adapter (`-j -G1 -a -n`).
/// </summary>
public sealed record MetadataSnapshot(
    string PathDisplay,
    IReadOnlyDictionary<string, string> Tags)
{
    /// <summary>EXIF Orientation 1–8 (including mirrored 2/4/5/7), or null when absent.</summary>
    public int? Orientation
        => Tags.TryGetValue("IFD0:Orientation", out var value)
            && int.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= 1 and <= 8
            ? parsed
            : null;
}

/// <summary>Field-level difference between two snapshots.</summary>
public sealed record MetadataDiff(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlyList<string> Changed)
{
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0;
}
