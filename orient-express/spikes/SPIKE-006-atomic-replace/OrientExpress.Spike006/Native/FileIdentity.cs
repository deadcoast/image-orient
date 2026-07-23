using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace OrientExpress.Spike006.Native;

/// <summary>Handle-based identity per DOC-FSSAFE: volume serial plus NTFS file index.</summary>
public readonly record struct FileIdentity(uint VolumeSerial, ulong FileIndex, uint LinkCount)
{
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{VolumeSerial:x8}:{FileIndex:x16}:links={LinkCount}");
}

public static class FileIdentityReader
{
    public static FileIdentity Read(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!NativeMethods.GetFileInformationByHandle(stream.SafeFileHandle, out var information))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"GetFileInformationByHandle failed for '{path}'");
        }

        var index = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return new FileIdentity(information.VolumeSerialNumber, index, information.NumberOfLinks);
    }
}
