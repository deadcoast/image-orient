namespace OrientExpress.Contracts.Imaging;

/// <summary>
/// Bounded header probe result (COMP-CODEC probe surface): format from
/// content signatures — never the file extension — plus header-derived
/// dimensions and EXIF orientation where the format carries them inline.
/// </summary>
public sealed record ImageFormatProbe(
    string Format,
    int? Width,
    int? Height,
    int? ExifOrientation,
    string? Warning);
