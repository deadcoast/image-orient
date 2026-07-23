using System.Runtime.InteropServices;
using OrientExpress.Application.Filesystem;
using OrientExpress.Contracts.Common;
using OrientExpress.Contracts.Filesystem;
using OrientExpress.Infrastructure.Windows.Native;

namespace OrientExpress.Infrastructure.Windows;

/// <summary>
/// The SPIKE-006-qualified atomic primitives (COMP-NATIVE). Error codes map
/// to the actionable classes measured in the fault matrix; there is no
/// silent fallback between primitives.
/// </summary>
public sealed class WindowsAtomicFileOperations : IAtomicFileOperations
{
    public Result<bool> ReplaceExisting(NormalizedPath target, NormalizedPath replacement)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(replacement);
        if (NativeMethods.ReplaceFile(
            target.Extended, replacement.Extended, null, NativeMethods.ReplaceWriteThrough, 0, 0))
        {
            return Result.Ok(true);
        }

        return MapError(Marshal.GetLastPInvokeError(), target.Display, "ReplaceFileW");
    }

    public Result<bool> MoveToNewDestination(NormalizedPath source, NormalizedPath destination)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        // No REPLACE_EXISTING: the create path must fail if the destination
        // appeared meanwhile (safe-copy never overwrites, DOC-FSSAFE).
        if (NativeMethods.MoveFileEx(source.Extended, destination.Extended, NativeMethods.MoveWriteThrough))
        {
            return Result.Ok(true);
        }

        return MapError(Marshal.GetLastPInvokeError(), destination.Display, "MoveFileEx");
    }

    private static Result<bool> MapError(int win32Error, string subject, string api)
        => win32Error switch
        {
            NativeMethods.ErrorSharingViolation => Result.Fail<bool>(
                "SHARING_VIOLATION", ErrorCategory.SharingViolation, true,
                $"'{subject}' is locked by another process"),
            NativeMethods.ErrorAccessDenied => Result.Fail<bool>(
                "ACCESS_DENIED", ErrorCategory.AccessDenied, false,
                $"access denied replacing '{subject}' (read-only or permissions)"),
            NativeMethods.ErrorFileNotFound => Result.Fail<bool>(
                "TARGET_NOT_FOUND", ErrorCategory.NotFound, false,
                $"'{subject}' does not exist"),
            NativeMethods.ErrorNotSameDevice => Result.Fail<bool>(
                "STAGE_NOT_SAME_VOLUME", ErrorCategory.CapabilityUnavailable, false,
                "stage and target must share a volume"),
            NativeMethods.ErrorAlreadyExists => Result.Fail<bool>(
                "DESTINATION_EXISTS", ErrorCategory.IntegrityViolation, false,
                $"'{subject}' already exists; safe-copy never overwrites"),
            _ => Result.Fail<bool>(
                "ATOMIC_OPERATION_FAILED", ErrorCategory.Unexpected, true,
                $"{api} failed with Win32 error {win32Error}"),
        };
}
