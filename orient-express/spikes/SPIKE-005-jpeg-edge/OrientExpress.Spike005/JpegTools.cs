using System.Diagnostics;
using System.Security.Cryptography;

namespace OrientExpress.Spike005;

public sealed record ToolResult(int ExitCode, string Stderr);

/// <summary>
/// Thin allowlisted wrappers over the libjpeg-turbo command-line tools
/// (DEP-008). Tool paths and digests are recorded in the evidence manifest.
/// </summary>
public sealed class JpegTools(string toolsDirectory)
{
    public string CjpegPath { get; } = Path.Combine(toolsDirectory, "cjpeg.exe");

    public string DjpegPath { get; } = Path.Combine(toolsDirectory, "djpeg.exe");

    public string JpegtranPath { get; } = Path.Combine(toolsDirectory, "jpegtran.exe");

    public string WrjpgcomPath { get; } = Path.Combine(toolsDirectory, "wrjpgcom.exe");

    public bool ToolsPresent =>
        File.Exists(CjpegPath) && File.Exists(DjpegPath) && File.Exists(JpegtranPath);

    public IReadOnlyDictionary<string, string> ToolDigests()
    {
        var digests = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in new[] { CjpegPath, DjpegPath, JpegtranPath })
        {
            using var stream = File.OpenRead(path);
            digests[Path.GetFileName(path)] = Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        return digests;
    }

    public ToolResult Encode(string ppmPath, string jpegPath, string sampling, bool progressive)
    {
        List<string> arguments = ["-quality", "90", "-sample", sampling];
        if (progressive)
        {
            arguments.Add("-progressive");
        }

        arguments.AddRange(["-outfile", jpegPath, ppmPath]);
        return Run(CjpegPath, arguments);
    }

    public ToolResult Decode(string jpegPath, string ppmPath)
        => Run(DjpegPath, ["-outfile", ppmPath, jpegPath]);

    public ToolResult Rotate(string inputPath, string outputPath, int degrees, string mode, string copyMarkers)
    {
        List<string> arguments = ["-copy", copyMarkers, "-rotate", degrees.ToString(System.Globalization.CultureInfo.InvariantCulture)];
        switch (mode)
        {
            case "perfect":
                arguments.Add("-perfect");
                break;
            case "trim":
                arguments.Add("-trim");
                break;
            default:
                break;
        }

        arguments.AddRange(["-outfile", outputPath, inputPath]);
        return Run(JpegtranPath, arguments);
    }

    /// <summary>wrjpgcom emits the annotated JPEG on stdout; capture it as raw bytes.</summary>
    public ToolResult AddComment(string inputPath, string outputPath, string comment)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = WrjpgcomPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-comment");
        startInfo.ArgumentList.Add(comment);
        startInfo.ArgumentList.Add(inputPath);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"failed to start {WrjpgcomPath}");
        using (var output = File.Create(outputPath))
        {
            process.StandardOutput.BaseStream.CopyTo(output);
        }

        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"{WrjpgcomPath} timed out");
        }

        return new ToolResult(process.ExitCode, stderr.Trim());
    }

    private static ToolResult Run(string toolPath, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = toolPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"failed to start {toolPath}");
        var stderr = process.StandardError.ReadToEnd();
        _ = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(60_000))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"{toolPath} timed out");
        }

        return new ToolResult(process.ExitCode, stderr.Trim());
    }
}
