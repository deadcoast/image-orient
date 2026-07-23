using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OrientExpress.Spike006.Native;

/// <summary>
/// The exact Win32 surface under qualification. DOC-FSSAFE: no API name is
/// treated as a guarantee; the fault matrix measures actual behavior.
/// </summary>
internal static partial class NativeMethods
{
    internal const uint ReplaceWriteThrough = 0x1;
    internal const uint MoveReplaceExisting = 0x1;
    internal const uint MoveWriteThrough = 0x8;
    internal const uint GenericWrite = 0x40000000;
    internal const uint ShareReadWriteDelete = 0x7;
    internal const uint OpenExisting = 3;
    internal const uint FlagBackupSemantics = 0x02000000;
    internal const uint DaclSecurityInformation = 0x4;
    internal const uint SddlRevision1 = 1;

    internal const int ErrorFileNotFound = 2;
    internal const int ErrorAccessDenied = 5;
    internal const int ErrorNotSameDevice = 17;
    internal const int ErrorSharingViolation = 32;
    internal const int ErrorInsufficientBuffer = 122;
    internal const int ErrorUnableToMoveReplacement2 = 1177;

    [StructLayout(LayoutKind.Sequential)]
    internal struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "ReplaceFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ReplaceFile(
        string replacedFileName,
        string replacementFileName,
        string? backupFileName,
        uint replaceFlags,
        nint exclude,
        nint reserved);

    [LibraryImport("kernel32.dll", EntryPoint = "MoveFileExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MoveFileEx(string existingFileName, string newFileName, uint flags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FlushFileBuffers(SafeFileHandle handle);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetFileInformationByHandle(SafeFileHandle handle, out ByHandleFileInformation information);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool GetVolumeInformationByHandleW(
        SafeFileHandle handle,
        char* volumeName,
        uint volumeNameSize,
        out uint serialNumber,
        out uint maximumComponentLength,
        out uint fileSystemFlags,
        char* fileSystemName,
        uint fileSystemNameSize);

    internal static unsafe bool TryGetVolumeInformation(
        SafeFileHandle handle, out string fileSystem, out uint serialNumber, out uint fileSystemFlags)
    {
        Span<char> volumeName = stackalloc char[261];
        Span<char> fileSystemName = stackalloc char[261];
        bool ok;
        fixed (char* volumePointer = volumeName)
        fixed (char* fileSystemPointer = fileSystemName)
        {
            ok = GetVolumeInformationByHandleW(
                handle,
                volumePointer,
                (uint)volumeName.Length,
                out serialNumber,
                out _,
                out fileSystemFlags,
                fileSystemPointer,
                (uint)fileSystemName.Length);
        }

        if (!ok)
        {
            fileSystem = string.Empty;
            return false;
        }

        var terminator = fileSystemName.IndexOf('\0');
        fileSystem = new string(fileSystemName[..(terminator < 0 ? fileSystemName.Length : terminator)]);
        return true;
    }

    [LibraryImport("advapi32.dll", EntryPoint = "GetFileSecurityW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileSecurity(
        string fileName,
        uint requestedInformation,
        byte[]? securityDescriptor,
        uint length,
        out uint lengthNeeded);

    [LibraryImport("advapi32.dll", EntryPoint = "ConvertSecurityDescriptorToStringSecurityDescriptorW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ConvertSecurityDescriptorToString(
        byte[] securityDescriptor,
        uint revision,
        uint securityInformation,
        out nint sddl,
        out uint sddlLength);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);

    /// <summary>Reads the DACL as an SDDL string for before/after comparison.</summary>
    internal static bool TryGetDaclSddl(string path, out string sddl)
    {
        sddl = string.Empty;
        if (GetFileSecurity(path, DaclSecurityInformation, null, 0, out var needed)
            || Marshal.GetLastPInvokeError() != ErrorInsufficientBuffer
            || needed == 0)
        {
            return false;
        }

        var descriptor = new byte[needed];
        if (!GetFileSecurity(path, DaclSecurityInformation, descriptor, needed, out _))
        {
            return false;
        }

        if (!ConvertSecurityDescriptorToString(descriptor, SddlRevision1, DaclSecurityInformation, out var pointer, out _))
        {
            return false;
        }

        try
        {
            sddl = Marshal.PtrToStringUni(pointer) ?? string.Empty;
            return true;
        }
        finally
        {
            _ = LocalFree(pointer);
        }
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(nint process, uint exitCode);

    /// <summary>
    /// Crash simulation: immediate kernel-level termination, no finalizers, no
    /// flushes, no Windows Error Reporting dialog — the closest user-mode
    /// analogue to power loss for buffered state.
    /// </summary>
    [DoesNotReturn]
    internal static void TerminateSelf(uint exitCode)
    {
        _ = TerminateProcess(GetCurrentProcess(), exitCode);
        Environment.FailFast("TerminateProcess returned unexpectedly.");
    }
}
