using System.Security.Cryptography;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OrientExpress.Application.Inference;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Models;

namespace OrientExpress.Infrastructure.Inference;

/// <summary>
/// COMP-INFERENCE adapter on the SPIKE-003-qualified baseline: ONNX Runtime,
/// CPU execution provider, deterministic. The artifact digest is re-verified
/// at load (DEP-006 rule) so a tampered file can never reach a session, and
/// sessions are cached by digest for the process lifetime.
/// </summary>
public sealed class OnnxInferenceRuntime : IInferenceRuntime
{
    private readonly Dictionary<string, InferenceSession> _sessions = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private bool _disposed;

    public RuntimeDescriptor Descriptor { get; } = new(
        "onnxruntime",
        typeof(InferenceSession).Assembly.GetName().Version?.ToString(3) ?? "unknown",
        "cpu");

    public ValueTask<Result<TensorOutput>> RunAsync(
        ModelHandle model,
        TensorInput input,
        InferenceOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var session = GetOrLoadSession(model);
            if (!session.IsSuccess)
            {
                return ValueTask.FromResult(Result.Fail<TensorOutput>(session.Error!));
            }

            var tensor = new DenseTensor<float>(input.Values, input.Shape);
            using var results = session.Require().Run(
                [NamedOnnxValue.CreateFromTensor(input.Name, tensor)]);
            var outputs = results.ToDictionary(
                value => value.Name,
                value => value.AsEnumerable<float>().ToArray(),
                StringComparer.Ordinal);
            return ValueTask.FromResult(Result.Ok(new TensorOutput(outputs)));
        }
        catch (OnnxRuntimeException exception)
        {
            return ValueTask.FromResult(Result.Fail<TensorOutput>(
                "INFERENCE_FAILED", ErrorCategory.InvalidInput, false, exception.Message));
        }
    }

    private Result<InferenceSession> GetOrLoadSession(ModelHandle model)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_sessions.TryGetValue(model.Sha256, out var cached))
            {
                return Result.Ok(cached);
            }

            if (!File.Exists(model.ArtifactPath))
            {
                return Result.Fail<InferenceSession>(
                    "MODEL_ARTIFACT_NOT_FOUND", ErrorCategory.NotFound, false,
                    $"no artifact at '{model.ArtifactPath}'");
            }

            using (var stream = File.OpenRead(model.ArtifactPath))
            {
                var digest = Convert.ToHexStringLower(SHA256.HashData(stream));
                if (digest != model.Sha256)
                {
                    return Result.Fail<InferenceSession>(
                        "MODEL_DIGEST_MISMATCH", ErrorCategory.IntegrityViolation, false,
                        $"artifact digest {digest} does not match the registered {model.Sha256}");
                }
            }

            var session = new InferenceSession(model.ArtifactPath);
            _sessions[model.Sha256] = session;
            return Result.Ok(session);
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }

            _sessions.Clear();
        }

        return ValueTask.CompletedTask;
    }
}
