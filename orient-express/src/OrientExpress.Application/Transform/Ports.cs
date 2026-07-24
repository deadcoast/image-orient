using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Transform;

namespace OrientExpress.Application.Transform;

/// <summary>
/// COMP-TRANSFORM port (DOC-COMPONENTS `IImageTransformer`): executes a
/// policy-authored plan against a source file into a stage target. The
/// transformer never writes to the source and never picks a fallback the
/// plan did not name.
/// </summary>
public interface IImageTransformer
{
    public ValueTask<Result<TransformArtifact>> TransformAsync(
        FileSnapshot source,
        StagingTarget target,
        TransformPlan plan,
        CancellationToken cancellationToken);
}
