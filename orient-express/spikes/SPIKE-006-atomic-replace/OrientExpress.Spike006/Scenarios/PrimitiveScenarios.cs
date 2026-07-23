using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using OrientExpress.Spike006.Native;
using OrientExpress.Spike006.Protocol;

namespace OrientExpress.Spike006.Scenarios;

/// <summary>
/// Qualifies the atomic primitives themselves (DOC-FSSAFE "Atomic primitive
/// qualification"): measured behavior of ReplaceFileW and MoveFileEx for
/// content, timestamps, attributes, ADS, DACL, identity, sharing, and error
/// codes. Measurements are evidence; only data-safety expectations hard-fail.
/// </summary>
public static class PrimitiveScenarios
{
    private const string Category = "primitive";
    private const string AdsName = "oxads";
    private const string GuestsSid = "S-1-5-32-546";

    public static IReadOnlyList<ScenarioResult> RunAll(string workRoot)
    {
        var root = Path.Combine(workRoot, "primitives");
        Directory.CreateDirectory(root);

        List<Func<string, ScenarioResult>> scenarios =
        [
            ReplaceBasic,
            ReplaceBackupParameter,
            ReplacePreservesTimestampsAttributesIdentity,
            ReplacePreservesAlternateDataStream,
            ReplacePreservesExplicitDaclEntry,
            ReplaceDeniedBySharingViolation,
            ReplaceAllowedByFullSharing,
            ReplaceReadOnlyTarget,
            ReplaceMissingTargetWithMoveFallback,
            MoveFileExSemanticsComparison,
            ReplaceAcrossVolumes,
            DirectoryFlushSupport,
        ];

        var results = new List<ScenarioResult>(scenarios.Count);
        foreach (var scenario in scenarios)
        {
            results.Add(RunIsolated(scenario, root));
        }

        return results;
    }

    private static ScenarioResult RunIsolated(Func<string, ScenarioResult> scenario, string root)
    {
        var directory = Path.Combine(root, scenario.Method.Name);
        Directory.CreateDirectory(directory);
        try
        {
            return scenario(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new ScenarioRecorder(scenario.Method.Name, Category, "unexpected harness failure").Failed(exception);
        }
    }

    private sealed record Fixture(string TargetPath, string ReplacementPath, string OriginalSha, string ExpectedSha);

    private static Fixture CreateFixture(string directory, string seed)
    {
        var original = JobFixture.DeterministicBytes(seed, 64 * 1024);
        var expected = ContentTransform.Apply(original);
        var target = Path.Combine(directory, "target.bin");
        var replacement = Path.Combine(directory, "replacement.bin");
        File.WriteAllBytes(target, original);
        File.WriteAllBytes(replacement, expected);
        return new Fixture(target, replacement, Digests.Sha256Hex(original), Digests.Sha256Hex(expected));
    }

    private static ScenarioResult ReplaceBasic(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-BASIC", Category, "ReplaceFileW swaps content atomically and consumes the replacement file");
        var fixture = CreateFixture(directory, "replace-basic");

        InPlaceCommitProtocol.Replace(fixture.TargetPath, fixture.ReplacementPath);

        recorder.ExpectSafety("target_has_expected_content", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.ExpectedSha);
        recorder.Expect("replacement_consumed", !File.Exists(fixture.ReplacementPath));
        return recorder.Complete();
    }

    private static ScenarioResult ReplaceBackupParameter(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-BACKUP-PARAM", Category, "lpBackupFileName receives the original content");
        var fixture = CreateFixture(directory, "replace-backup-param");
        var backupPath = Path.Combine(directory, "api-backup.bin");

        var ok = NativeMethods.ReplaceFile(
            fixture.TargetPath, fixture.ReplacementPath, backupPath, NativeMethods.ReplaceWriteThrough, 0, 0);
        recorder.Expect("replace_succeeded", ok);
        recorder.ExpectSafety("target_has_expected_content", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.ExpectedSha);
        recorder.ExpectSafety(
            "api_backup_holds_original", File.Exists(backupPath) && Digests.Sha256HexOfFile(backupPath) == fixture.OriginalSha);
        return recorder.Complete();
    }

    private static ScenarioResult ReplacePreservesTimestampsAttributesIdentity(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-PRESERVATION", Category,
            "measured retention of creation time, attributes, and file identity across ReplaceFileW");
        var fixture = CreateFixture(directory, "replace-preservation");

        var knownCreation = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetCreationTimeUtc(fixture.TargetPath, knownCreation);
        File.SetAttributes(fixture.TargetPath, File.GetAttributes(fixture.TargetPath) | FileAttributes.Hidden);
        var identityBefore = FileIdentityReader.Read(fixture.TargetPath);
        var writeTimeOfReplacement = File.GetLastWriteTimeUtc(fixture.ReplacementPath);

        InPlaceCommitProtocol.Replace(fixture.TargetPath, fixture.ReplacementPath);

        var identityAfter = FileIdentityReader.Read(fixture.TargetPath);
        var creationAfter = File.GetCreationTimeUtc(fixture.TargetPath);
        var attributesAfter = File.GetAttributes(fixture.TargetPath);
        recorder.ExpectSafety("target_has_expected_content", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.ExpectedSha);
        recorder.Measure("creation_time_preserved", creationAfter == knownCreation);
        recorder.Measure("creation_time_after", creationAfter.ToString("O", CultureInfo.InvariantCulture));
        recorder.Measure("hidden_attribute_preserved", attributesAfter.HasFlag(FileAttributes.Hidden));
        recorder.Measure("file_id_before", identityBefore.ToString());
        recorder.Measure("file_id_after", identityAfter.ToString());
        recorder.Measure("file_id_preserved", identityBefore.VolumeSerial == identityAfter.VolumeSerial
            && identityBefore.FileIndex == identityAfter.FileIndex);
        recorder.Measure(
            "last_write_time_from_replacement",
            File.GetLastWriteTimeUtc(fixture.TargetPath) == writeTimeOfReplacement);
        File.SetAttributes(fixture.TargetPath, attributesAfter & ~FileAttributes.Hidden);
        return recorder.Complete();
    }

    private static ScenarioResult ReplacePreservesAlternateDataStream(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-ADS", Category, "measured retention of a named alternate data stream on the replaced file");
        var fixture = CreateFixture(directory, "replace-ads");
        File.WriteAllText(fixture.TargetPath + ":" + AdsName, "spike006-ads-payload");

        InPlaceCommitProtocol.Replace(fixture.TargetPath, fixture.ReplacementPath);

        recorder.ExpectSafety("target_has_expected_content", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.ExpectedSha);
        recorder.Measure("ads_preserved", TryReadAds(fixture.TargetPath, out var payload));
        recorder.Measure("ads_payload_intact", payload == "spike006-ads-payload");
        return recorder.Complete();
    }

    private static bool TryReadAds(string path, out string payload)
    {
        try
        {
            payload = File.ReadAllText(path + ":" + AdsName);
            return true;
        }
        catch (FileNotFoundException)
        {
            payload = string.Empty;
            return false;
        }
    }

    private static ScenarioResult ReplacePreservesExplicitDaclEntry(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-DACL", Category, "measured retention of an explicit DACL entry across ReplaceFileW");
        var fixture = CreateFixture(directory, "replace-dacl");

        if (!TryGrantGuestsRead(fixture.TargetPath))
        {
            return recorder.Skip("icacls could not add an explicit ACE; insufficient rights on this volume");
        }

        _ = NativeMethods.TryGetDaclSddl(fixture.TargetPath, out var sddlBefore);
        InPlaceCommitProtocol.Replace(fixture.TargetPath, fixture.ReplacementPath);
        _ = NativeMethods.TryGetDaclSddl(fixture.TargetPath, out var sddlAfter);

        recorder.ExpectSafety("target_has_expected_content", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.ExpectedSha);
        recorder.Measure("explicit_ace_in_before_sddl", ContainsGuestsAce(sddlBefore));
        recorder.Measure("explicit_ace_preserved", ContainsGuestsAce(sddlAfter));
        recorder.Measure("dacl_sddl_unchanged", sddlBefore == sddlAfter);
        // Digests, not raw SDDL: local account SIDs stay out of committed evidence.
        recorder.Measure("dacl_sddl_before_sha256", Digests.Sha256Hex(Encoding.Unicode.GetBytes(sddlBefore)));
        recorder.Measure("dacl_sddl_after_sha256", Digests.Sha256Hex(Encoding.Unicode.GetBytes(sddlAfter)));
        return recorder.Complete();
    }

    /// <summary>SDDL renders well-known SIDs as aliases, so Guests appears as "BG", not its raw SID.</summary>
    private static bool ContainsGuestsAce(string sddl)
        => sddl.Contains(GuestsSid, StringComparison.Ordinal) || sddl.Contains(";BG)", StringComparison.Ordinal);

    private static bool TryGrantGuestsRead(string path)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "icacls",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("/grant");
        startInfo.ArgumentList.Add("*" + GuestsSid + ":R");
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static ScenarioResult ReplaceDeniedBySharingViolation(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-SHARE-DENIED", Category,
            "an open handle without delete sharing must block ReplaceFileW and leave the target untouched");
        var fixture = CreateFixture(directory, "replace-share-denied");

        bool ok;
        int error;
        using (new FileStream(fixture.TargetPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            ok = NativeMethods.ReplaceFile(
                fixture.TargetPath, fixture.ReplacementPath, null, NativeMethods.ReplaceWriteThrough, 0, 0);
            error = Marshal.GetLastPInvokeError();
        }

        recorder.Expect("replace_rejected", !ok);
        recorder.Measure("win32_error", error);
        recorder.Measure(
            "error_is_sharing_or_replacement_class",
            error is NativeMethods.ErrorSharingViolation or NativeMethods.ErrorAccessDenied
                or NativeMethods.ErrorUnableToMoveReplacement2);
        recorder.ExpectSafety("target_unchanged", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.OriginalSha);
        recorder.ExpectSafety("replacement_preserved", File.Exists(fixture.ReplacementPath));
        return recorder.Complete();
    }

    private static ScenarioResult ReplaceAllowedByFullSharing(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-SHARE-FULL", Category,
            "measured behavior when the target is open with read/write/delete sharing");
        var fixture = CreateFixture(directory, "replace-share-full");

        bool ok;
        int error;
        using (new FileStream(
            fixture.TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            ok = NativeMethods.ReplaceFile(
                fixture.TargetPath, fixture.ReplacementPath, null, NativeMethods.ReplaceWriteThrough, 0, 0);
            error = Marshal.GetLastPInvokeError();
        }

        recorder.Measure("replace_succeeded", ok);
        recorder.Measure("win32_error", ok ? 0 : error);
        var digest = Digests.Sha256HexOfFile(fixture.TargetPath);
        recorder.ExpectSafety("content_is_original_or_expected", digest == fixture.OriginalSha || digest == fixture.ExpectedSha);
        return recorder.Complete();
    }

    private static ScenarioResult ReplaceReadOnlyTarget(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-READONLY", Category, "measured behavior against a read-only target");
        var fixture = CreateFixture(directory, "replace-readonly");
        File.SetAttributes(fixture.TargetPath, File.GetAttributes(fixture.TargetPath) | FileAttributes.ReadOnly);
        try
        {
            var ok = NativeMethods.ReplaceFile(
                fixture.TargetPath, fixture.ReplacementPath, null, NativeMethods.ReplaceWriteThrough, 0, 0);
            var error = Marshal.GetLastPInvokeError();
            recorder.Measure("replace_succeeded", ok);
            recorder.Measure("win32_error", ok ? 0 : error);
            var digest = Digests.Sha256HexOfFile(fixture.TargetPath);
            recorder.ExpectSafety(
                "content_is_original_or_expected", digest == fixture.OriginalSha || digest == fixture.ExpectedSha);
        }
        finally
        {
            File.SetAttributes(fixture.TargetPath, File.GetAttributes(fixture.TargetPath) & ~FileAttributes.ReadOnly);
        }

        return recorder.Complete();
    }

    private static ScenarioResult ReplaceMissingTargetWithMoveFallback(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-MISSING-TARGET", Category,
            "ReplaceFileW requires an existing target; MoveFileEx(write-through) is the create-path fallback");
        var fixture = CreateFixture(directory, "replace-missing-target");
        File.Delete(fixture.TargetPath);

        var ok = NativeMethods.ReplaceFile(
            fixture.TargetPath, fixture.ReplacementPath, null, NativeMethods.ReplaceWriteThrough, 0, 0);
        var error = Marshal.GetLastPInvokeError();
        recorder.Expect("replace_rejected", !ok);
        recorder.Measure("win32_error", error);
        recorder.Measure("error_is_file_not_found", error == NativeMethods.ErrorFileNotFound);

        var moved = NativeMethods.MoveFileEx(
            fixture.ReplacementPath,
            fixture.TargetPath,
            NativeMethods.MoveReplaceExisting | NativeMethods.MoveWriteThrough);
        recorder.Expect("movefileex_fallback_succeeded", moved);
        recorder.ExpectSafety("target_has_expected_content", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.ExpectedSha);
        return recorder.Complete();
    }

    private static ScenarioResult MoveFileExSemanticsComparison(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-MOVEFILEEX-COMPARISON", Category,
            "MoveFileEx(REPLACE_EXISTING) comparison: what the alternative primitive fails to preserve");
        var fixture = CreateFixture(directory, "movefileex-comparison");

        var knownCreation = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetCreationTimeUtc(fixture.TargetPath, knownCreation);
        File.WriteAllText(fixture.TargetPath + ":" + AdsName, "spike006-ads-payload");
        var identityBefore = FileIdentityReader.Read(fixture.TargetPath);

        var moved = NativeMethods.MoveFileEx(
            fixture.ReplacementPath,
            fixture.TargetPath,
            NativeMethods.MoveReplaceExisting | NativeMethods.MoveWriteThrough);
        recorder.Expect("movefileex_succeeded", moved);
        recorder.ExpectSafety("target_has_expected_content", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.ExpectedSha);

        var identityAfter = FileIdentityReader.Read(fixture.TargetPath);
        recorder.Measure("creation_time_preserved", File.GetCreationTimeUtc(fixture.TargetPath) == knownCreation);
        recorder.Measure("ads_preserved", TryReadAds(fixture.TargetPath, out _));
        recorder.Measure("file_id_preserved", identityBefore.VolumeSerial == identityAfter.VolumeSerial
            && identityBefore.FileIndex == identityAfter.FileIndex);
        return recorder.Complete();
    }

    private static ScenarioResult ReplaceAcrossVolumes(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-REPLACE-CROSS-VOLUME", Category,
            "same-volume staging is mandatory: ReplaceFileW must reject a cross-volume replacement");
        var fixture = CreateFixture(directory, "replace-cross-volume");

        var currentRoot = Path.GetPathRoot(Path.GetFullPath(directory));
        var otherVolume = DriveInfo.GetDrives().FirstOrDefault(drive =>
            drive.DriveType == DriveType.Fixed
            && drive.IsReady
            && !string.Equals(drive.RootDirectory.FullName, currentRoot, StringComparison.OrdinalIgnoreCase));
        if (otherVolume is null)
        {
            return recorder.Skip(
                "no second fixed volume on this machine; cross-volume rejection stays capability-gated (DOC-FSSAFE)");
        }

        var foreignReplacement = Path.Combine(
            otherVolume.RootDirectory.FullName, "spike006-cross-volume-" + Path.GetFileName(fixture.ReplacementPath));
        try
        {
            File.Copy(fixture.ReplacementPath, foreignReplacement, overwrite: true);
            var ok = NativeMethods.ReplaceFile(
                fixture.TargetPath, foreignReplacement, null, NativeMethods.ReplaceWriteThrough, 0, 0);
            var error = Marshal.GetLastPInvokeError();
            recorder.Expect("replace_rejected", !ok);
            recorder.Measure("win32_error", error);
            recorder.Measure("error_is_not_same_device", error == NativeMethods.ErrorNotSameDevice);
            recorder.ExpectSafety("target_unchanged", Digests.Sha256HexOfFile(fixture.TargetPath) == fixture.OriginalSha);
        }
        catch (UnauthorizedAccessException)
        {
            return recorder.Skip("second volume is not writable from this account");
        }
        finally
        {
            if (File.Exists(foreignReplacement))
            {
                File.Delete(foreignReplacement);
            }
        }

        return recorder.Complete();
    }

    private static ScenarioResult DirectoryFlushSupport(string directory)
    {
        var recorder = new ScenarioRecorder(
            "P-DIR-FLUSH", Category, "directory-handle FlushFileBuffers support on this volume");
        recorder.Measure("directory_flush_succeeded", DirectoryFlush.BestEffort(directory));
        return recorder.Complete();
    }
}
