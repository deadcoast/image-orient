using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Models;

namespace OrientExpress.Application.Inference;

/// <summary>
/// COMP-MODELS port (PRD-FR-020): digest-pinned bundle lifecycle. Every
/// failure path quarantines rather than installs (AC-038); activation is
/// atomic and records the prior model for rollback (AC-039).
/// </summary>
public interface IModelRegistry
{
    public Result<IReadOnlyList<InstalledModel>> List();

    public Result<InstalledModel> Install(string sourceArtifactPath, ModelManifest manifest);

    public Result<InstalledModel> Verify(string modelId, string version);

    public Result<InstalledModel> Activate(string modelId, string version);

    public Result<InstalledModel> Rollback();

    public Result<bool> Remove(string modelId, string version);

    public Result<InstalledModel?> GetActive();
}

/// <summary>COMP-INFERENCE port (DOC-COMPONENTS `IInferenceRuntime`).</summary>
public interface IInferenceRuntime : IAsyncDisposable
{
    public RuntimeDescriptor Descriptor { get; }

    public ValueTask<Result<TensorOutput>> RunAsync(
        ModelHandle model,
        TensorInput input,
        InferenceOptions options,
        CancellationToken cancellationToken);
}
