---
document_id: DOC-CONTRACTS
title: Component Contracts
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-ARCH, DOC-DATAMODEL, DOC-CONFIG]
traces_to: [COMP-CLI, COMP-CONFIG, COMP-CAPABILITIES, COMP-DISCOVERY, COMP-METADATA, COMP-CODEC, COMP-TRANSFORM, COMP-INFERENCE, COMP-MODELS, COMP-FEATURES, COMP-CALIBRATION, COMP-POLICY, COMP-ORIENTATION, COMP-PLANNER, COMP-PIPELINE, COMP-EXECUTOR, COMP-VERIFY, COMP-FILESYSTEM, COMP-JOURNAL, COMP-JOBSTORE, COMP-REVIEW, COMP-REPORTING, COMP-BENCHMARK, COMP-NATIVE]
---

# Component contracts

Contracts below are conceptual C# shapes. Exact namespaces and record fields may evolve through implementation, but responsibility and error semantics are normative.

## Common result model

```csharp
public readonly record struct Result<T>(
    bool IsSuccess,
    T? Value,
    ErrorInfo? Error,
    IReadOnlyList<WarningInfo> Warnings);

public sealed record ErrorInfo(
    string Code,
    ErrorCategory Category,
    bool IsRetryable,
    string SafeMessage,
    IReadOnlyDictionary<string, JsonElement> Details);
```

Exceptions are reserved for programmer defects, cancellation, or failures that cannot be represented at the current boundary. Expected untrusted-input and capability failures are typed results.

## File discovery and identity

```csharp
public interface IInputDiscovery
{
    IAsyncEnumerable<DiscoveredInput> DiscoverAsync(
        DiscoveryRequest request,
        CancellationToken cancellationToken);
}

public interface IFileIdentityService
{
    ValueTask<Result<FileSnapshot>> CaptureAsync(
        NormalizedPath path,
        FingerprintStrength strength,
        CancellationToken cancellationToken);

    ValueTask<Result<SnapshotComparison>> CompareAsync(
        FileSnapshot expected,
        CancellationToken cancellationToken);
}
```

`FileSnapshot` includes normalized display path, handle-derived volume/file ID when available, size, last-write timestamp, selected content digests, reparse/link/cloud attributes, and filesystem capability class. Paths alone are not identities.

## Metadata

```csharp
public interface IMetadataService : IAsyncDisposable
{
    ValueTask<Result<MetadataSnapshot>> ReadAsync(
        FileSnapshot file,
        MetadataReadProfile profile,
        CancellationToken cancellationToken);

    ValueTask<Result<MetadataWriteResult>> ApplyAsync(
        StagedFile file,
        MetadataMutation mutation,
        MetadataPreservationPolicy policy,
        CancellationToken cancellationToken);

    ValueTask<Result<MetadataDiff>> CompareAsync(
        MetadataSnapshot before,
        FileSnapshot after,
        MetadataVerificationPolicy policy,
        CancellationToken cancellationToken);
}
```

No caller constructs arbitrary ExifTool arguments. The adapter maps typed fields/policies to an allowlisted protocol and synchronizes each `-execute<ID>` response.

## Codec and transform

```csharp
public interface IImageProbe
{
    ValueTask<Result<ImageDescriptor>> ProbeAsync(FileSnapshot file, CancellationToken ct);
}

public interface IThumbnailDecoder
{
    ValueTask<Result<DecodedImage>> DecodeBoundedAsync(
        FileSnapshot file,
        DecodeRequest request,
        CancellationToken ct);
}

public interface IImageTransformer
{
    ValueTask<Result<TransformArtifact>> TransformAsync(
        FileSnapshot source,
        StagingTarget target,
        TransformPlan plan,
        CancellationToken ct);
}
```

`TransformPlan` is created by policy and includes the exact composed transform, JPEG edge policy, raster encoder policy, metadata mutation, expected dimensions, and verification contract. The transformer cannot choose a fallback silently.

## Inference and orientation

```csharp
public interface IInferenceRuntime : IAsyncDisposable
{
    RuntimeDescriptor Descriptor { get; }
    ValueTask<Result<TensorOutput>> RunAsync(
        ModelHandle model,
        TensorInput input,
        InferenceOptions options,
        CancellationToken ct);
}

public interface IOrientationEngine
{
    ValueTask<Result<OrientationAssessment>> AssessAsync(
        AnalysisInput input,
        OrientationPolicy policy,
        CancellationToken ct);
}

public interface IConfidenceCalibrator
{
    CalibratedDecision Calibrate(
        FeatureVector features,
        CalibrationArtifact calibration);
}
```

`OrientationAssessment` retains all four candidate summaries, raw and calibrated values, selected action, confidence/margin, evidence sufficiency, reason codes, and model/runtime/policy provenance.

## Planning and job store

```csharp
public interface IJobStore : IAsyncDisposable
{
    ValueTask<JobHandle> CreateAsync(JobManifest manifest, CancellationToken ct);
    ValueTask AppendEventsAsync(JobId job, IReadOnlyList<JobEvent> events, CancellationToken ct);
    ValueTask UpsertScanBatchAsync(JobId job, IReadOnlyList<PlannedFile> files, CancellationToken ct);
    ValueTask SealPlanAsync(JobId job, PlanSeal seal, CancellationToken ct);
    IAsyncEnumerable<PlannedFile> ReadPlanAsync(JobId job, PlanQuery query, CancellationToken ct);
    ValueTask<IntegrityReport> CheckIntegrityAsync(JobId job, CancellationToken ct);
}

public interface IPlanner
{
    Task<ScanSummary> ScanAsync(ScanRequest request, IProgressSink progress, CancellationToken ct);
}
```

A sealed plan is content-addressed. Normal commands cannot mutate planned evidence/action rows. Review decisions are append-only overlays with actor/source/time/schema and are included in an updated decision seal.

## Execution, verification, commit

```csharp
public interface IApplyExecutor
{
    Task<ApplySummary> ApplyAsync(ApplyRequest request, IProgressSink progress, CancellationToken ct);
}

public interface IOutputVerifier
{
    ValueTask<VerificationResult> VerifyAsync(
        VerificationContract contract,
        FileSnapshot stagedOutput,
        CancellationToken ct);
}

public interface ICommitService
{
    ValueTask<CommitResult> CommitSafeCopyAsync(VerifiedArtifact artifact, CancellationToken ct);
    ValueTask<CommitResult> CommitInPlaceAsync(
        VerifiedArtifact artifact,
        VerifiedBackup backup,
        JournalIntent intent,
        CancellationToken ct);
}
```

The commit service accepts only verified artifacts and durable journal intents. It does not receive raw transform output.

## Native process supervision

```csharp
public interface INativeToolSupervisor
{
    ValueTask<Result<TResponse>> ExecuteAsync<TRequest,TResponse>(
        ToolIdentity tool,
        TRequest request,
        ToolExecutionPolicy policy,
        CancellationToken ct);
}
```

Policy includes executable digest, working directory, environment allowlist, input/output limits, timeout, kill-tree behavior, protocol framing, stderr capture/redaction, and concurrency.

## Events and progress

Events are durable facts; progress is ephemeral UI. Event creation is part of application state transitions, whereas human progress rendering may be disabled. Machine output serializes events from the stable event contract.

## Contract testing

Every adapter has:

- a conformance test suite against a reference fake;
- golden success/error/protocol cases;
- cancellation and timeout cases;
- version/capability probes;
- malicious/untrusted input cases;
- provenance capture;
- a replacement test demonstrating that application/domain tests run without the production adapter.
