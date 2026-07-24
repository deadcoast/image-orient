using System.Diagnostics;
using OrientExpress.Application.Metadata;
using OrientExpress.Application.Transform;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Transform;

namespace OrientExpress.Infrastructure.Imaging;

/// <summary>
/// COMP-TRANSFORM adapter for the lossless JPEG path (PRD-FR-011,
/// ADR-0007): executes the plan's DCT-domain operation via jpegtran
/// (DEP-008) into the exclusive stage file, then sets the orientation tag
/// on the stage copy through the metadata adapter (PRD-FR-012; the WBS
/// binding is `-copy all` plus metadata via ExifTool, never jpegtran's own
/// marker handling). The source file is never written.
/// </summary>
public sealed class JpegtranTransformer(string jpegtranPath, IMetadataService metadataService) : IImageTransformer
{
    private const int ToolTimeoutMilliseconds = 60_000;

    public async ValueTask<Result<TransformArtifact>> TransformAsync(
        FileSnapshot source,
        StagingTarget target,
        TransformPlan plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(plan);

        var sourcePath = source.Path.Display;
        var stagePath = target.Path.Display;
        if (!IsJpeg(sourcePath))
        {
            return Result.Fail<TransformArtifact>(
                "TRANSFORM_INPUT_NOT_JPEG",
                ErrorCategory.InvalidInput,
                isRetryable: false,
                "The lossless JPEG path requires a JPEG source (SOI marker missing).");
        }

        if (File.Exists(stagePath))
        {
            return Result.Fail<TransformArtifact>(
                "TRANSFORM_STAGE_CONFLICT",
                ErrorCategory.InvalidInput,
                isRetryable: false,
                "The stage path already exists; stage files must be exclusively created (DOC-FSSAFE).");
        }

        if (plan.Operation == JpegOperation.None)
        {
            File.Copy(sourcePath, stagePath);
        }
        else
        {
            var arguments = JpegtranArguments.Build(plan, sourcePath, stagePath);
            if (!arguments.IsSuccess)
            {
                return Result.Fail<TransformArtifact>(arguments.Error!);
            }

            if (!File.Exists(jpegtranPath))
            {
                return Result.Fail<TransformArtifact>(
                    "TRANSFORM_TOOL_MISSING",
                    ErrorCategory.CapabilityUnavailable,
                    isRetryable: false,
                    "jpegtran was not found; install libjpeg-turbo (DEP-008).");
            }

            var run = await RunToolAsync(arguments.Require(), cancellationToken).ConfigureAwait(false);
            if (run.ExitCode != 0 || !File.Exists(stagePath) || new FileInfo(stagePath).Length < 4)
            {
                DeleteQuietly(stagePath);
                var firstStderrLine = run.Stderr.Split('\n', 2)[0].Trim();
                return plan.EdgePolicy == JpegEdgePolicy.Abort && run.ExitCode != 0
                    ? Result.Fail<TransformArtifact>(
                        "JPEG_EDGE_NOT_PERFECT",
                        ErrorCategory.InvalidInput,
                        isRetryable: false,
                        $"The abort policy refuses non-MCU-aligned edges: {firstStderrLine}")
                    : Result.Fail<TransformArtifact>(
                        "TRANSFORM_TOOL_FAILED",
                        ErrorCategory.Unexpected,
                        isRetryable: false,
                        $"jpegtran exit {run.ExitCode}: {firstStderrLine}");
            }
        }

        var orientationWritten = false;
        if (plan.SetOrientationTo > 0)
        {
            var write = await metadataService
                .WriteOrientationAsync(target.Path, plan.SetOrientationTo, cancellationToken)
                .ConfigureAwait(false);
            if (!write.IsSuccess)
            {
                DeleteQuietly(stagePath);
                return Result.Fail<TransformArtifact>(write.Error!);
            }

            orientationWritten = true;
        }

        return Result.Ok(new TransformArtifact(
            target.Path,
            plan.Operation,
            plan.EdgePolicy,
            CoefficientLossless: plan.Operation == JpegOperation.None || plan.EdgePolicy == JpegEdgePolicy.Abort,
            orientationWritten));
    }

    private static bool IsJpeg(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[2];
            return stream.Read(header) == 2 && header[0] == 0xFF && header[1] == 0xD8;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private async Task<(int ExitCode, string Stderr)> RunToolAsync(
        IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = jpegtranPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"failed to start {jpegtranPath}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ToolTimeoutMilliseconds);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        _ = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException($"{jpegtranPath} timed out after {ToolTimeoutMilliseconds} ms");
        }

        return (process.ExitCode, await stderrTask.ConfigureAwait(false));
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The stage file is orphan-cleanup's problem if deletion races.
        }
    }
}
