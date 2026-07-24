using System.Diagnostics;
using OrientExpress.Application.Filesystem;
using OrientExpress.Contracts.Verify;
using OrientExpress.Infrastructure.Imaging;

namespace OrientExpress.InfrastructureTests;

/// <summary>
/// AC-025 against real files: the verifier reopens the output with the
/// independent header prober and rejects missing, truncated, non-image,
/// wrong-size, and wrong-orientation outputs — collecting every failure,
/// not just the first.
/// </summary>
public sealed class ProbeOutputVerifierTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("upright-verify-tests-").FullName;

    private readonly ProbeOutputVerifier _verifier = new(new ManagedFormatProber());

    private static VerificationContract Contract(int width = 96, int height = 64)
        => new("jpeg", width, height, ExpectedOrientation: 1);

    private async Task<VerificationResult> Verify(string path, VerificationContract? contract = null)
        => (await _verifier.VerifyAsync(
            contract ?? Contract(), PathCanonicalizer.Normalize(path), CancellationToken.None)).Require();

    /// <summary>Encodes a real width-by-height JPEG via cjpeg (skips when absent).</summary>
    private string CreateJpeg(int width, int height)
    {
        var cjpeg = @"C:\libjpeg-turbo64\bin\cjpeg.exe";
        if (!File.Exists(cjpeg))
        {
            Assert.Skip("cjpeg (libjpeg-turbo) is not installed on this machine");
        }

        var ppm = Path.Combine(_directory, $"f-{width}x{height}.ppm");
        var jpeg = Path.Combine(_directory, $"f-{width}x{height}.jpg");
        using (var stream = File.Create(ppm))
        {
            stream.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
            stream.Write(new byte[width * height * 3]);
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = cjpeg,
            ArgumentList = { "-outfile", jpeg, ppm },
            UseShellExecute = false,
            RedirectStandardError = true,
        });
        Assert.True(process!.WaitForExit(30_000));
        return jpeg;
    }

    [Fact]
    public async Task Correct_output_passes()
    {
        var result = await Verify(CreateJpeg(96, 64));
        Assert.True(result.Passed);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task Missing_output_fails_with_the_stable_code()
    {
        var result = await Verify(Path.Combine(_directory, "never-written.jpg"));
        Assert.False(result.Passed);
        Assert.Equal([VerificationFailureCodes.OutputMissing], result.Failures);
    }

    [Fact]
    public async Task Non_image_bytes_fail_as_wrong_format()
    {
        var path = Path.Combine(_directory, "junk.jpg");
        File.WriteAllBytes(path, new byte[64]);
        var result = await Verify(path);
        Assert.False(result.Passed);
        Assert.Contains(VerificationFailureCodes.OutputWrongFormat, result.Failures);
    }

    [Fact]
    public async Task Wrong_dimensions_fail_and_every_failure_is_collected()
    {
        var jpeg = CreateJpeg(32, 32);
        var result = await Verify(jpeg, Contract(width: 96, height: 64));
        Assert.False(result.Passed);
        Assert.Contains(VerificationFailureCodes.OutputWrongDimensions, result.Failures);

        var tiny = await Verify(jpeg, Contract(width: 96, height: 64) with { MinimumBytes = long.MaxValue });
        Assert.Contains(VerificationFailureCodes.OutputTooSmall, tiny.Failures);
        Assert.Contains(VerificationFailureCodes.OutputWrongDimensions, tiny.Failures);
    }

    [Fact]
    public async Task Wrong_orientation_fails()
    {
        var jpeg = CreateJpeg(96, 64);
        var result = await Verify(jpeg, Contract() with { ExpectedOrientation = 6 });
        Assert.False(result.Passed);
        Assert.Equal([VerificationFailureCodes.OutputWrongOrientation], result.Failures);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
