using System.Runtime.InteropServices;

namespace OrientExpress.Infrastructure.Windows;

/// <summary>
/// Hardlink creation for fixtures and future discovery-time alias handling
/// (DOC-FSSAFE: deduplicate hardlink aliases during planning).
/// </summary>
public static partial class WindowsHardLinks
{
    public static bool TryCreate(string newLinkPath, string existingFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newLinkPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(existingFilePath);
        return CreateHardLink(newLinkPath, existingFilePath, 0);
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateHardLinkW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateHardLink(string fileName, string existingFileName, nint securityAttributes);
}
