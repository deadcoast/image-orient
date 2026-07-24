using System.Text.Json.Serialization;
using OrientExpress.Application.Filesystem;
using OrientExpress.Application.Metadata;
using OrientExpress.Cli.Hosting;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Infrastructure.Windows;

namespace OrientExpress.Cli.Commands;

/// <summary>
/// `upright inspect <file>` alpha (PRD-FR-002/PRD-FR-003, M1 exit
/// deliverable): identity snapshot, bounded format probe, and EXIF
/// orientation — with the decision engine explicitly gated (WP-007). Reads
/// only; never writes anything anywhere.
/// </summary>
public static class InspectCommand
{
    public static int Run(
        OutputContext context,
        string filePath,
        IMetadataService metadataService,
        IImageFormatProber prober,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(metadataService);
        ArgumentNullException.ThrowIfNull(prober);
        if (!File.Exists(filePath))
        {
            context.WriteError(
                CliExitCode.InvalidArgument,
                "FILE_NOT_FOUND",
                $"no file at '{filePath}'",
                "pass an existing image file");
            return (int)CliExitCode.InvalidArgument;
        }

        var path = PathCanonicalizer.Normalize(filePath);
        var blockers = new List<string>();

        var identity = new WindowsFileIdentityService()
            .CaptureAsync(path, FingerprintStrength.Standard, cancellationToken)
            .AsTask().GetAwaiter().GetResult();
        if (!identity.IsSuccess)
        {
            blockers.Add($"identity: {identity.Error!.Code}");
        }

        var probe = prober.Probe(path);
        if (!probe.IsSuccess)
        {
            blockers.Add($"format: {probe.Error!.Code}");
        }

        var metadata = metadataService.ReadAsync(path, cancellationToken).AsTask().GetAwaiter().GetResult();
        if (!metadata.IsSuccess)
        {
            blockers.Add($"metadata: {metadata.Error!.Code}");
        }

        var snapshot = identity.IsSuccess ? identity.Require() : null;
        var format = probe.IsSuccess ? probe.Require() : null;
        var document = new InspectDocument
        {
            Command = "inspect",
            ContractVersion = CliHost.ContractVersion,
            CorrelationId = context.CorrelationId,
            Path = path.Display,
            Format = format?.Format,
            Width = format?.Width,
            Height = format?.Height,
            ExifOrientation = metadata.IsSuccess ? metadata.Require().Orientation : format?.ExifOrientation,
            FileIdHex = snapshot?.FileIdHex,
            VolumeSerialHex = snapshot?.VolumeSerialHex,
            SizeBytes = snapshot?.Fingerprint.SizeBytes,
            CapabilityClass = snapshot?.Capability.Class.ToString(),
            MetadataTagCount = metadata.IsSuccess ? metadata.Require().Tags.Count : null,
            Decision = new InspectDecision
            {
                Available = false,
                ReasonCodes = ["DECISION_ENGINE_GATED"],
                NextAction = "orientation decisions arrive with WP-007/WP-008 after the corpus and model spikes",
            },
            Blockers = blockers,
        };

        if (context.Machine)
        {
            MachineWriter.WriteDocument(context, document);
        }
        else
        {
            context.Out.WriteLine($"{CliHost.ExecutableName} inspect — read-only diagnostic view");
            context.Out.WriteLine($"  path: {document.Path}");
            context.Out.WriteLine($"  format: {document.Format ?? "unreadable"}"
                + (document.Width is not null ? $" ({document.Width}x{document.Height})" : string.Empty));
            context.Out.WriteLine($"  exif orientation: {document.ExifOrientation?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "absent/unreadable"}");
            if (snapshot is not null)
            {
                context.Out.WriteLine($"  identity: volume {document.VolumeSerialHex} file {document.FileIdHex} ({document.SizeBytes} bytes, {document.CapabilityClass})");
            }

            context.Out.WriteLine("  decision: gated — " + document.Decision.NextAction);
            foreach (var blocker in blockers)
            {
                context.Out.WriteLine($"  blocker: {blocker}");
            }
        }

        return (int)CliExitCode.Success;
    }

    private sealed record InspectDecision
    {
        [JsonPropertyName("available")]
        public required bool Available { get; init; }

        [JsonPropertyName("reasonCodes")]
        public required IReadOnlyList<string> ReasonCodes { get; init; }

        [JsonPropertyName("nextAction")]
        public required string NextAction { get; init; }
    }

    private sealed record InspectDocument
    {
        [JsonPropertyName("schemaVersion")]
        public string SchemaVersion { get; init; } = "1.0";

        [JsonPropertyName("resultType")]
        public string ResultType { get; init; } = "inspect.result";

        [JsonPropertyName("command")]
        public required string Command { get; init; }

        [JsonPropertyName("contractVersion")]
        public required string ContractVersion { get; init; }

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; init; }

        [JsonPropertyName("path")]
        public required string Path { get; init; }

        [JsonPropertyName("format")]
        public string? Format { get; init; }

        [JsonPropertyName("width")]
        public int? Width { get; init; }

        [JsonPropertyName("height")]
        public int? Height { get; init; }

        [JsonPropertyName("exifOrientation")]
        public int? ExifOrientation { get; init; }

        [JsonPropertyName("fileIdHex")]
        public string? FileIdHex { get; init; }

        [JsonPropertyName("volumeSerialHex")]
        public string? VolumeSerialHex { get; init; }

        [JsonPropertyName("sizeBytes")]
        public long? SizeBytes { get; init; }

        [JsonPropertyName("capabilityClass")]
        public string? CapabilityClass { get; init; }

        [JsonPropertyName("metadataTagCount")]
        public int? MetadataTagCount { get; init; }

        [JsonPropertyName("decision")]
        public required InspectDecision Decision { get; init; }

        [JsonPropertyName("blockers")]
        public required IReadOnlyList<string> Blockers { get; init; }
    }
}
