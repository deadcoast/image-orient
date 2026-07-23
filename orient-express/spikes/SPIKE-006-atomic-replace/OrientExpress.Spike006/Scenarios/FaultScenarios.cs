using System.Diagnostics;
using System.Text;
using OrientExpress.Spike006.Protocol;
using OrientExpress.Spike006.Recovery;

namespace OrientExpress.Spike006.Scenarios;

/// <summary>
/// The crash fault matrix (DOC-FSSAFE recovery matrix): a child process runs
/// the in-place protocol and is kernel-terminated at each step boundary; the
/// parent then observes, recovers, and checks the zero-source-loss invariant.
/// </summary>
public static class FaultScenarios
{
    private const string Category = "fault-injection";
    private static readonly TimeSpan ChildTimeout = TimeSpan.FromSeconds(60);

    private sealed record FaultCase(
        string Id,
        FaultPoint Fault,
        string Description,
        RecoveryAction ExpectedAction,
        string ExpectedFinalSource,
        Action<JobManifest>? AfterCrash);

    private const string FinalOriginal = "original";
    private const string FinalExpected = "expected";
    private const string FinalTampered = "tampered";

    public static IReadOnlyList<ScenarioResult> RunAll(string workRoot)
    {
        var root = Path.Combine(workRoot, "faults");
        Directory.CreateDirectory(root);

        List<FaultCase> cases =
        [
            new("F-BASELINE", FaultPoint.None,
                "no fault: protocol completes; post-run recovery is a terminal no-op",
                RecoveryAction.FinalizeIdempotent, FinalExpected, AfterCrash: null),
            new("F-AFTER-BACKUP", FaultPoint.AfterBackupCreated,
                "crash after verified backup, before staging",
                RecoveryAction.QuarantineStageSourceAuthoritative, FinalOriginal, AfterCrash: null),
            new("F-AFTER-STAGE-CREATED", FaultPoint.AfterStageCreated,
                "crash right after exclusive stage creation (stage empty)",
                RecoveryAction.QuarantineStageSourceAuthoritative, FinalOriginal, AfterCrash: null),
            new("F-AFTER-STAGE-WRITTEN", FaultPoint.AfterStageWritten,
                "crash after stage write but before flush",
                RecoveryAction.QuarantineStageSourceAuthoritative, FinalOriginal, AfterCrash: null),
            new("F-AFTER-STAGE-FLUSHED", FaultPoint.AfterStageFlushed,
                "crash after stage flush and verification, before durable intent",
                RecoveryAction.QuarantineStageSourceAuthoritative, FinalOriginal, AfterCrash: null),
            new("F-AFTER-INTENT", FaultPoint.AfterIntentDurable,
                "crash after durable intent, before replacement: recovery must finish the job",
                RecoveryAction.RetryReplacement, FinalExpected, AfterCrash: null),
            new("F-AFTER-REPLACE", FaultPoint.AfterReplace,
                "crash between replacement and verification",
                RecoveryAction.RecordRecoveredCommit, FinalExpected, AfterCrash: null),
            new("F-AFTER-VERIFY", FaultPoint.AfterVerify,
                "crash between verification and the commit record",
                RecoveryAction.RecordRecoveredCommit, FinalExpected, AfterCrash: null),
            new("F-AFTER-COMMITTED", FaultPoint.AfterCommitted,
                "crash after durable commit, before cleanup",
                RecoveryAction.FinalizeIdempotent, FinalExpected, AfterCrash: null),
            new("F-INTENT-STAGE-CORRUPT", FaultPoint.AfterIntentDurable,
                "durable intent but the stage was damaged before recovery ran",
                RecoveryAction.StopActionableStageError, FinalOriginal, AfterCrash: CorruptStage),
            new("F-INTENT-SOURCE-TAMPER", FaultPoint.AfterIntentDurable,
                "durable intent but the source was changed externally: critical conflict, everything preserved",
                RecoveryAction.StopCriticalConflict, FinalTampered, AfterCrash: TamperSource),
            new("F-COMMITTED-SOURCE-TAMPER", FaultPoint.AfterCommitted,
                "post-commit external change: report drift, never overwrite",
                RecoveryAction.ReportDriftWithoutOverwrite, FinalTampered, AfterCrash: TamperSource),
        ];

        var results = new List<ScenarioResult>(cases.Count);
        foreach (var faultCase in cases)
        {
            results.Add(Run(root, faultCase));
        }

        return results;
    }

    private static byte[] TamperContent(JobManifest manifest)
        => JobFixture.DeterministicBytes(manifest.JobId + "/tamper", 32 * 1024);

    private static void TamperSource(JobManifest manifest)
        => File.WriteAllBytes(manifest.SourcePath, TamperContent(manifest));

    private static void CorruptStage(JobManifest manifest)
    {
        var pattern = Path.GetFileName(manifest.SourcePath) + InPlaceCommitProtocol.StageMarker + "*";
        foreach (var stagePath in Directory.GetFiles(manifest.SourceDirectory, pattern))
        {
            File.WriteAllBytes(stagePath, Encoding.ASCII.GetBytes("corrupted-by-fault-scenario"));
        }
    }

    private static ScenarioResult Run(string root, FaultCase faultCase)
    {
        var recorder = new ScenarioRecorder(faultCase.Id, Category, faultCase.Description);
        try
        {
            var jobDirectory = Path.Combine(root, faultCase.Id);
            var manifest = JobFixture.Create(jobDirectory, faultCase.Id);

            var exitCode = RunProtocolInChildProcess(jobDirectory, faultCase.Fault);
            var expectedExit = faultCase.Fault == FaultPoint.None ? 0 : FaultInjector.CrashExitCode;
            recorder.Measure("child_exit_code", exitCode);
            recorder.Expect("child_exit_as_injected", exitCode == expectedExit);

            faultCase.AfterCrash?.Invoke(manifest);

            var outcome = RecoveryExecutor.Recover(manifest);
            recorder.Measure("observed_checkpoint", outcome.Observation.Checkpoint.ToString());
            recorder.Measure("observed_source", outcome.Observation.Source.ToString());
            recorder.Measure("observed_stage", outcome.Observation.Stage.ToString());
            recorder.Measure("observed_backup", outcome.Observation.Backup.ToString());
            recorder.Measure("recovery_action", outcome.Action.ToString());
            recorder.Measure("recovery_notes", outcome.Notes);
            recorder.Expect("recovery_action_matches_matrix", outcome.Action == faultCase.ExpectedAction);

            CheckFinalState(recorder, manifest, faultCase.ExpectedFinalSource);
            CheckIdempotency(recorder, manifest);
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return recorder.Failed(exception);
        }

        return recorder.Complete();
    }

    private static void CheckFinalState(ScenarioRecorder recorder, JobManifest manifest, string expectedFinalSource)
    {
        var sourceDigest = File.Exists(manifest.SourcePath) ? Digests.Sha256HexOfFile(manifest.SourcePath) : "missing";
        var backupIntact = File.Exists(manifest.BackupPath)
            && Digests.Sha256HexOfFile(manifest.BackupPath) == manifest.OriginalSha256;
        var expectedDigest = expectedFinalSource switch
        {
            FinalOriginal => manifest.OriginalSha256,
            FinalExpected => manifest.ExpectedSha256,
            FinalTampered => Digests.Sha256Hex(TamperContent(manifest)),
            _ => throw new InvalidOperationException($"Unknown expected final source '{expectedFinalSource}'."),
        };

        recorder.Measure("final_source_class", expectedFinalSource);
        recorder.ExpectSafety("final_source_state_as_expected", sourceDigest == expectedDigest);

        // The zero-source-loss gate: original bytes stay recoverable from the
        // source itself or from the verified backup, in every injected fault.
        var originalRecoverable = sourceDigest == manifest.OriginalSha256 || backupIntact;
        recorder.ExpectSafety("original_content_recoverable", originalRecoverable);
        recorder.Measure("backup_intact", backupIntact);
    }

    private static void CheckIdempotency(ScenarioRecorder recorder, JobManifest manifest)
    {
        var digestBefore = Digests.Sha256HexOfFile(manifest.SourcePath);
        var second = RecoveryExecutor.Recover(manifest);
        recorder.Measure("second_recovery_action", second.Action.ToString());
        recorder.Expect("second_recovery_left_source_unchanged", Digests.Sha256HexOfFile(manifest.SourcePath) == digestBefore);
        recorder.Expect(
            "second_recovery_is_non_mutating_class",
            second.Action is RecoveryAction.FinalizeIdempotent
                or RecoveryAction.ReportDriftWithoutOverwrite
                or RecoveryAction.StopActionableStageError
                or RecoveryAction.StopCriticalConflict
                or RecoveryAction.QuarantineStageSourceAuthoritative);
    }

    private static int RunProtocolInChildProcess(string jobDirectory, FaultPoint fault)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath
                ?? throw new InvalidOperationException("Environment.ProcessPath is unavailable."),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("run-protocol");
        startInfo.ArgumentList.Add("--job");
        startInfo.ArgumentList.Add(jobDirectory);
        startInfo.ArgumentList.Add("--fault");
        startInfo.ArgumentList.Add(fault.ToString());

        using var child = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the protocol child process.");
        if (!child.WaitForExit((int)ChildTimeout.TotalMilliseconds))
        {
            child.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Protocol child process timed out.");
        }

        return child.ExitCode;
    }
}
