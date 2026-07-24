using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Contracts.Verify;

namespace OrientExpress.Application.Verify;

/// <summary>
/// COMP-VERIFY port (PRD-FR-013): reopen a staged output through an
/// independent read path and compare it against the plan's expectations.
/// A failed verification prevents commit (AC-026); the verifier itself
/// never deletes or moves anything.
/// </summary>
public interface IOutputVerifier
{
    public ValueTask<Result<VerificationResult>> VerifyAsync(
        VerificationContract contract,
        NormalizedPath stagedOutput,
        CancellationToken cancellationToken);
}
