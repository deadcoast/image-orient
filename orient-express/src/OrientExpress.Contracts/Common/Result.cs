namespace OrientExpress.Contracts.Common;

public enum ErrorCategory
{
    InvalidInput,
    NotFound,
    AccessDenied,
    SharingViolation,
    CapabilityUnavailable,
    IntegrityViolation,
    Cancelled,
    Unexpected,
}

/// <summary>
/// DOC-COMPONENTS common result model: expected untrusted-input and
/// capability failures are typed results; exceptions are reserved for
/// programmer defects and cancellation.
/// </summary>
public sealed record ErrorInfo(
    string Code,
    ErrorCategory Category,
    bool IsRetryable,
    string SafeMessage);

public readonly record struct Result<T>
{
    internal Result(bool isSuccess, T? value, ErrorInfo? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public ErrorInfo? Error { get; }

    /// <summary>Success value, or throws for programmer misuse on failure paths.</summary>
    public T Require()
        => IsSuccess && Value is not null
            ? Value
            : throw new InvalidOperationException(
                $"Result accessed without success: {Error?.Code ?? "UNKNOWN"} — {Error?.SafeMessage}");
}

public static class Result
{
    public static Result<T> Ok<T>(T value) => new(true, value, null);

    public static Result<T> Fail<T>(ErrorInfo error) => new(false, default, error);

    public static Result<T> Fail<T>(string code, ErrorCategory category, bool isRetryable, string safeMessage)
        => Fail<T>(new ErrorInfo(code, category, isRetryable, safeMessage));
}
