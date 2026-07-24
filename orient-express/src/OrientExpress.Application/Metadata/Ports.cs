using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Imaging;
using OrientExpress.Contracts.Metadata;

namespace OrientExpress.Application.Metadata;

/// <summary>
/// COMP-METADATA port — the diagnostic-mode subset of the DOC-COMPONENTS
/// `IMetadataService` shape. The only mutation is the SPIKE-004-proven
/// orientation write; policy-governed mutation arrives with WP-010.
/// </summary>
public interface IMetadataService : IDisposable
{
    /// <summary>Adapter identity for capability truth-telling: version and binary digest.</summary>
    public Result<(string Version, string Sha256)> Identify();

    public ValueTask<Result<MetadataSnapshot>> ReadAsync(NormalizedPath path, CancellationToken cancellationToken);

    public ValueTask<Result<bool>> WriteOrientationAsync(
        NormalizedPath path, int orientationValue, CancellationToken cancellationToken);

    public ValueTask<Result<MetadataDiff>> CompareAsync(
        MetadataSnapshot before, NormalizedPath path, CancellationToken cancellationToken);
}

/// <summary>COMP-CODEC probe port: bounded header inspection, no pixel decode.</summary>
public interface IImageFormatProber
{
    public Result<ImageFormatProbe> Probe(NormalizedPath path);
}
