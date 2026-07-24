using System.Text.Json;
using OrientExpress.Cli.Hosting;
using OrientExpress.Contracts.Cli;

namespace OrientExpress.CliTests;

/// <summary>
/// `upright inspect` alpha golden tests (PRD-FR-002/003, M1 exit). Assertions
/// tolerate ExifTool presence or absence: the metadata section degrades to a
/// typed blocker, never a crash.
/// </summary>
public sealed class InspectCommandTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-inspect-tests-").FullName;

    private static (int ExitCode, string Stdout, string Stderr) Invoke(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = CliHost.Invoke(args, stdout, stderr);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private string WriteMinimalJpeg()
    {
        // SOI + SOF0(64x48) + EOI: enough for the bounded prober.
        var path = Path.Combine(_directory, "photo.jpg");
        File.WriteAllBytes(path,
        [
            0xFF, 0xD8,
            0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x30, 0x00, 0x40, 0x01, 0x01, 0x11, 0x00,
            0xFF, 0xD9,
        ]);
        return path;
    }

    [Fact]
    public void Missing_file_is_refused_with_a_typed_error()
    {
        var result = Invoke("inspect", Path.Combine(_directory, "absent.jpg"));
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.Contains("FILE_NOT_FOUND", result.Stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void Inspect_json_reports_format_identity_and_a_gated_decision()
    {
        var result = Invoke("inspect", WriteMinimalJpeg(), "--json", "--non-interactive");
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);

        using var document = JsonDocument.Parse(result.Stdout);
        var root = document.RootElement;
        Assert.Equal("inspect.result", root.GetProperty("resultType").GetString());
        Assert.Equal("jpeg", root.GetProperty("format").GetString());
        Assert.Equal(64, root.GetProperty("width").GetInt32());
        Assert.Equal(48, root.GetProperty("height").GetInt32());
        Assert.True(root.TryGetProperty("fileIdHex", out _));
        Assert.False(root.GetProperty("decision").GetProperty("available").GetBoolean());
        Assert.Contains(
            "DECISION_ENGINE_GATED",
            root.GetProperty("decision").GetProperty("reasonCodes").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public void Inspect_human_first_line_and_gated_decision_render()
    {
        var result = Invoke("inspect", WriteMinimalJpeg(), "--non-interactive");
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("upright inspect — read-only diagnostic view", result.Stdout, StringComparison.Ordinal);
        Assert.Contains("decision: gated", result.Stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Doctor_reports_format_prober_available_and_baseline_read_true()
    {
        var result = Invoke("doctor", "--json", "--non-interactive");
        using var document = JsonDocument.Parse(result.Stdout);
        var components = document.RootElement.GetProperty("components").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetString()!, c => c.GetProperty("available").GetBoolean());
        Assert.True(components["format-prober"]);

        var jpeg = document.RootElement.GetProperty("formats").EnumerateArray()
            .Single(f => f.GetProperty("format").GetString() == "jpeg");
        Assert.True(jpeg.GetProperty("read").GetBoolean());
        // metadataRead mirrors ExifTool presence on this machine; both are valid truths.
        Assert.Equal(
            components["metadata-exiftool"],
            jpeg.GetProperty("metadataRead").GetBoolean());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
