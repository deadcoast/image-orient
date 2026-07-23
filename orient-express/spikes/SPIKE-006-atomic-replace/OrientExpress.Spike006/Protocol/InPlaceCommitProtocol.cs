using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OrientExpress.Spike006.Native;

namespace OrientExpress.Spike006.Protocol;

/// <summary>
/// The DOC-FSSAFE in-place protocol under test: verified backup, same-directory
/// stage, flush, durable intent, qualified replace, independent verify, durable
/// commit. Every step boundary is a fault-injection point.
/// </summary>
public static class InPlaceCommitProtocol
{
    public const string StageMarker = ".oxstage-";

    public static void Run(JobManifest manifest, FaultInjector faults)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(faults);

        var original = File.ReadAllBytes(manifest.SourcePath);
        Require(Digests.Sha256Hex(original) == manifest.OriginalSha256, "source does not match the manifest");
        var expected = ContentTransform.Apply(original);
        Require(Digests.Sha256Hex(expected) == manifest.ExpectedSha256, "transform does not match the manifest");

        WriteDurable(manifest.BackupPath, original);
        Require(
            Digests.Sha256HexOfFile(manifest.BackupPath) == manifest.OriginalSha256,
            "backup failed independent verification");
        faults.Trip(FaultPoint.AfterBackupCreated);

        var stagePath = manifest.SourcePath + StageMarker + RandomSuffix();
        using (var stage = new FileStream(stagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            faults.Trip(FaultPoint.AfterStageCreated);
            stage.Write(expected);
            faults.Trip(FaultPoint.AfterStageWritten);
            stage.Flush(flushToDisk: true);
        }

        Require(Digests.Sha256HexOfFile(stagePath) == manifest.ExpectedSha256, "stage failed independent verification");
        faults.Trip(FaultPoint.AfterStageFlushed);

        var journal = new CommitJournal(manifest.JournalPath);
        var record = new JournalRecord(
            Checkpoint: nameof(JournalCheckpoint.None),
            JobId: manifest.JobId,
            SourcePath: manifest.SourcePath,
            StagePath: stagePath,
            BackupPath: manifest.BackupPath,
            OriginalSha256: manifest.OriginalSha256,
            ExpectedSha256: manifest.ExpectedSha256);

        journal.Append(JournalCheckpoint.IntentDurable, record);
        faults.Trip(FaultPoint.AfterIntentDurable);

        Replace(manifest.SourcePath, stagePath);
        faults.Trip(FaultPoint.AfterReplace);

        Require(
            Digests.Sha256HexOfFile(manifest.SourcePath) == manifest.ExpectedSha256,
            "replaced source failed independent verification");
        faults.Trip(FaultPoint.AfterVerify);

        journal.Append(JournalCheckpoint.Committed, record);
        faults.Trip(FaultPoint.AfterCommitted);

        _ = DirectoryFlush.BestEffort(manifest.SourceDirectory);
        journal.Append(JournalCheckpoint.Finalized, record);
    }

    /// <summary>The qualified atomic primitive: write-through ReplaceFileW, no silent fallback.</summary>
    public static void Replace(string targetPath, string replacementPath)
    {
        if (!NativeMethods.ReplaceFile(targetPath, replacementPath, null, NativeMethods.ReplaceWriteThrough, 0, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"ReplaceFileW failed for '{targetPath}'");
        }
    }

    private static string RandomSuffix()
        => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(6));

    private static void WriteDurable(string path, byte[] content)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(content);
        stream.Flush(flushToDisk: true);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
