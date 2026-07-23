using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OrientExpress.Infrastructure.Windows.Native;

/// <summary>
/// The Win32 surface qualified by SPIKE-006. Every use site maps error codes
/// to typed results; no API here is treated as a guarantee beyond the
/// recorded fault-matrix evidence.
/// </summary>
internal static partial class NativeMethods
{
    internal const uint ReplaceWriteThrough = 0x1;
    internal const uint MoveWriteThrough = 0x8;

    internal const int ErrorFileNotFound = 2;
    internal const int ErrorAccessDenied = 5;
    internal const int ErrorNotSameDevice = 17;
    internal const int ErrorSharingViolation = 32;
    internal const int ErrorAlreadyExists = 183;

    internal const uint AttributeReparsePoint = 0x0400;
    internal const uint AttributeOffline = 0x1000;
    internal const uint AttributeRecallOnOpen = 0x40000;
    internal const uint AttributeRecallOnDataAccess = 0x400000;

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
        SafeFileHandle handle, out string fileSystem, out uint serialNumber)
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
                out _,
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
}
