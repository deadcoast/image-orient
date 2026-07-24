using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OrientExpress.Infrastructure.Metadata.ExifTool;

public sealed record ExifToolResponse(int ExecuteId, string Output, string StderrOutput);

/// <summary>
/// The SPIKE-004-qualified `-stay_open` protocol (ADR-0006): one persistent
/// process, arguments one per line, each command synchronized on its
/// `-execute<id>`/`{ready<id>}` pair on stdout AND an `-echo4` sentinel on
/// stderr so diagnostics are attributable per command. Lazy start,
/// single-writer via lock, allowlisted operations only.
/// </summary>
public sealed class ExifToolStayOpenClient(string exifToolPath) : IDisposable
{
    private readonly Lock _gate = new();
    private readonly BlockingCollection<string> _stderrLines = [];
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _nextExecuteId;
    private bool _disposed;

    public string ExifToolPath { get; } = exifToolPath;

    public string Version() => Execute("-ver").Output.Trim();

    /// <summary>Complete grouped numeric tag dump as JSON (`-j -G1 -a -n`).</summary>
    public string ReadAllTagsJson(string path)
        => Execute("-j", "-G1", "-a", "-n", path).Output;

    /// <summary>The one governed mutation in diagnostic mode (SPIKE-004 proven).</summary>
    public ExifToolResponse WriteOrientation(string path, int orientationValue)
        => Execute(
            "-n",
            string.Create(CultureInfo.InvariantCulture, $"-EXIF:Orientation={orientationValue}"),
            "-overwrite_original",
            "-P",
            path);

    private ExifToolResponse Execute(params string[] arguments)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureStarted();
            var executeId = ++_nextExecuteId;
            var stderrSentinel = string.Create(CultureInfo.InvariantCulture, $"{{readyerr{executeId}}}");
            foreach (var argument in arguments)
            {
                _stdin!.Write(argument);
                _stdin.Write('\n');
            }

            _stdin!.Write("-echo4\n");
            _stdin.Write(stderrSentinel);
            _stdin.Write('\n');
            _stdin.Write(string.Create(CultureInfo.InvariantCulture, $"-execute{executeId}\n"));
            _stdin.Flush();

            var readyMarker = string.Create(CultureInfo.InvariantCulture, $"{{ready{executeId}}}");
            var output = new StringBuilder();
            while (true)
            {
                var line = _stdout!.ReadLine()
                    ?? throw new InvalidOperationException("exiftool terminated mid-command");
                if (line.StartsWith(readyMarker, StringComparison.Ordinal))
                {
                    break;
                }

                output.AppendLine(line);
            }

            var stderr = new StringBuilder();
            while (_stderrLines.TryTake(out var errorLine, TimeSpan.FromSeconds(5)))
            {
                if (errorLine.StartsWith(stderrSentinel, StringComparison.Ordinal))
                {
                    break;
                }

                stderr.AppendLine(errorLine);
            }

            return new ExifToolResponse(executeId, output.ToString(), stderr.ToString());
        }
    }

    private void EnsureStarted()
    {
        if (_process is not null)
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ExifToolPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardInputEncoding = new UTF8Encoding(false),
        };
        startInfo.ArgumentList.Add("-stay_open");
        startInfo.ArgumentList.Add("True");
        startInfo.ArgumentList.Add("-@");
        startInfo.ArgumentList.Add("-");
        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("failed to start exiftool");
        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;
        _process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                _stderrLines.Add(eventArgs.Data);
            }
        };
        _process.BeginErrorReadLine();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_process is null)
            {
                return;
            }

            try
            {
                _stdin!.Write("-stay_open\nFalse\n");
                _stdin.Flush();
                if (!_process.WaitForExit(5_000))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
                // Process already gone.
            }

            _process.Dispose();
        }
    }
}
