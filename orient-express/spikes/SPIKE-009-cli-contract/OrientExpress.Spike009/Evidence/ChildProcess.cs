using System.Diagnostics;
using System.Text;

namespace OrientExpress.Spike009.Evidence;

public sealed record ChildResult(int ExitCode, byte[] StdoutBytes, string Stderr)
{
    public string StdoutStrictUtf8()
        => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetString(StdoutBytes);

    public bool StdoutHasBom()
        => StdoutBytes.Length >= 3
            && StdoutBytes[0] == 0xEF && StdoutBytes[1] == 0xBB && StdoutBytes[2] == 0xBF;
}

/// <summary>
/// Spawns the harness itself with real redirected pipes so encoding, BOM, and
/// stream-separation behavior is measured out-of-process, byte-accurate.
/// </summary>
public static class ChildProcess
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public static ChildResult Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath
                ?? throw new InvalidOperationException("Environment.ProcessPath is unavailable."),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var child = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the harness child process.");
        using var stdout = new MemoryStream();
        var stdoutCopy = child.StandardOutput.BaseStream.CopyToAsync(stdout);
        var stderrTask = child.StandardError.ReadToEndAsync();
        if (!child.WaitForExit((int)Timeout.TotalMilliseconds))
        {
            child.Kill(entireProcessTree: true);
            throw new InvalidOperationException("Harness child process timed out.");
        }

        stdoutCopy.GetAwaiter().GetResult();
        return new ChildResult(child.ExitCode, stdout.ToArray(), stderrTask.GetAwaiter().GetResult());
    }
}
