using OrientExpress.Spike006.Native;

namespace OrientExpress.Spike006.Protocol;

/// <summary>Crash boundaries between the durable steps of the in-place protocol.</summary>
public enum FaultPoint
{
    None,
    AfterBackupCreated,
    AfterStageCreated,
    AfterStageWritten,
    AfterStageFlushed,
    AfterIntentDurable,
    AfterReplace,
    AfterVerify,
    AfterCommitted,
}

public sealed class FaultInjector(FaultPoint target)
{
    /// <summary>Distinctive exit code so the parent can tell an injected crash from a real failure.</summary>
    public const int CrashExitCode = 42;

    public void Trip(FaultPoint point)
    {
        if (point == target && point != FaultPoint.None)
        {
            NativeMethods.TerminateSelf(CrashExitCode);
        }
    }
}
