using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace OrientExpress.Spike004;

/// <summary>
/// Prototype of the COMP-METADATA adapter protocol (ADR-0006): one
/// persistent `exiftool -stay_open True -@ -` process, arguments written one
/// per line, and every command synchronized on its own `-execute<id>` /
/// `{ready<id>}` pair. Callers never construct raw argument strings; the
/// public surface is typed operations only.
/// </summary>
public sealed class ExifToolStayOpenClient : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private readonly BlockingCollection<string> _stderrLines = [];
    private int _nextExecuteId;

    public ExifToolStayOpenClient(string exifToolPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exifToolPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = exifToolPath,
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

    public int ProcessId => _process.Id;

    public string Version() => Execute("-ver").Output.Trim();

    /// <summary>Complete grouped numeric tag dump as JSON (`-j -G1 -a -n`).</summary>
    public string ReadAllTagsJson(string path)
        => Execute("-j", "-G1", "-a", "-n", path).Output;

    /// <summary>The one production write the orientation pipeline needs.</summary>
    public ExifToolResponse WriteOrientation(string path, int orientationValue, bool preserveFileTime)
    {
        List<string> arguments =
        [
            "-n",
            string.Create(CultureInfo.InvariantCulture, $"-EXIF:Orientation={orientationValue}"),
            "-overwrite_original",
        ];
        if (preserveFileTime)
        {
            arguments.Add("-P");
        }

        arguments.Add(path);
        return Execute([.. arguments]);
    }

    /// <summary>Fixture builder only: not part of the adapter surface.</summary>
    public ExifToolResponse WriteFixtureTags(string path, IReadOnlyDictionary<string, string> tags)
    {
        List<string> arguments = ["-overwrite_original"];
        arguments.AddRange(tags.Select(tag => $"-{tag.Key}={tag.Value}"));
        arguments.Add(path);
        return Execute([.. arguments]);
    }

    /// <summary>Deliberately invalid operation for error-channel evidence.</summary>
    public ExifToolResponse WriteInvalidTag(string path)
        => Execute("-NotARealTagName=1", "-overwrite_original", path);

    private ExifToolResponse Execute(params string[] arguments)
    {
        var executeId = ++_nextExecuteId;
        var stderrSentinel = string.Create(CultureInfo.InvariantCulture, $"{{readyerr{executeId}}}");
        foreach (var argument in arguments)
        {
            _stdin.Write(argument);
            _stdin.Write('\n');
        }

        // -echo4 emits a per-command sentinel on stderr AFTER processing, so
        // diagnostics synchronize exactly like stdout does with {ready<id>}.
        _stdin.Write("-echo4\n");
        _stdin.Write(stderrSentinel);
        _stdin.Write('\n');
        _stdin.Write(string.Create(CultureInfo.InvariantCulture, $"-execute{executeId}\n"));
        _stdin.Flush();

        var readyMarker = string.Create(CultureInfo.InvariantCulture, $"{{ready{executeId}}}");
        var output = new StringBuilder();
        while (true)
        {
            var line = _stdout.ReadLine()
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

    public void Dispose()
    {
        try
        {
            _stdin.Write("-stay_open\nFalse\n");
            _stdin.Flush();
            if (!_process.WaitForExit(5_000))
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (IOException)
        {
            // Process already gone.
        }

        _process.Dispose();
    }
}

public sealed record ExifToolResponse(int ExecuteId, string Output, string StderrOutput);
