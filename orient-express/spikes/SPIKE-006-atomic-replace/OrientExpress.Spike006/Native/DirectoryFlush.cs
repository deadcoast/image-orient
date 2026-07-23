namespace OrientExpress.Spike006.Native;

public static class DirectoryFlush
{
    /// <summary>
    /// Flushes directory metadata after a commit. Best effort by design: the
    /// journal, not the directory entry, is the durable source of truth.
    /// </summary>
    public static bool BestEffort(string directory)
    {
        using var handle = NativeMethods.CreateFile(
            directory,
            NativeMethods.GenericWrite,
            NativeMethods.ShareReadWriteDelete,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FlagBackupSemantics,
            0);
        return !handle.IsInvalid && NativeMethods.FlushFileBuffers(handle);
    }
}
