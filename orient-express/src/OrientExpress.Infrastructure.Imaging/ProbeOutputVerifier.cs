using OrientExpress.Application.Metadata;
using OrientExpress.Application.Verify;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Verify;

namespace OrientExpress.Infrastructure.Imaging;

/// <summary>
/// COMP-VERIFY adapter (PRD-FR-013, AC-025): reopens the staged output
/// with the bounded managed header prober — a genuinely independent read
/// path from the tools that produced the file (jpegtran wrote the
/// coefficients, ExifTool wrote the tag; neither parses the result here) —
/// and rejects decode failure, wrong format, wrong dimensions, or wrong
/// orientation. All failures are collected, not first-only, so a report
/// says everything that is wrong with an output.
/// </summary>
public sealed class ProbeOutputVerifier(IImageFormatProber prober) : IOutputVerifier
{
    public ValueTask<Result<VerificationResult>> VerifyAsync(
        VerificationContract contract,
        NormalizedPath stagedOutput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(stagedOutput);
        cancellationToken.ThrowIfCancellationRequested();

        var failures = new List<string>();
        var info = new FileInfo(stagedOutput.Display);
        if (!info.Exists)
        {
            return Done([VerificationFailureCodes.OutputMissing]);
        }

        if (info.Length < contract.MinimumBytes)
        {
            failures.Add(VerificationFailureCodes.OutputTooSmall);
        }

        var probe = prober.Probe(stagedOutput);
        if (!probe.IsSuccess)
        {
            failures.Add(VerificationFailureCodes.OutputDecodeFailed);
            return Done(failures);
        }

        var image = probe.Require();
        if (!string.Equals(image.Format, contract.ExpectedFormat, StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(VerificationFailureCodes.OutputWrongFormat);
        }

        if (image.Width is null || image.Height is null)
        {
            failures.Add(VerificationFailureCodes.OutputDecodeFailed);
        }
        else if (image.Width != contract.ExpectedWidth || image.Height != contract.ExpectedHeight)
        {
            failures.Add(VerificationFailureCodes.OutputWrongDimensions);
        }

        // Orientation absent counts as 1 (the JPEG default reading).
        var orientation = image.ExifOrientation ?? 1;
        if (orientation != contract.ExpectedOrientation)
        {
            failures.Add(VerificationFailureCodes.OutputWrongOrientation);
        }

        return Done(failures);
    }

    private static ValueTask<Result<VerificationResult>> Done(List<string> failures)
        => ValueTask.FromResult(Result.Ok(
            failures.Count == 0 ? VerificationResult.Pass : new VerificationResult(false, failures)));
}
