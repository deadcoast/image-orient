using System.Text;
using OrientExpress.Cli.Hosting;
using OrientExpress.Contracts.Cli;

// Machine output must be UTF-8 without BOM even when redirected: a redirected
// Windows console otherwise defaults to the legacy ANSI/OEM codepage
// (SPIKE-009 finding; NFR-UX-002).
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    // First Ctrl+C requests graceful cancellation (exit 40); a second one
    // falls through to the runtime's hard termination.
    if (!cancellation.IsCancellationRequested)
    {
        eventArgs.Cancel = true;
        cancellation.Cancel();
    }
};

try
{
    return CliHost.Invoke(args, Console.Out, Console.Error, cancellation.Token);
}
catch (Exception exception)
{
    // Last-resort backstop so the process always exits within the registry.
    Console.Error.WriteLine(
        $"error [{CliExitCodeRegistry.SymbolOf(CliExitCode.InternalError)}] [UNEXPECTED_FAILURE]: " +
        $"{exception.Message}; next: re-run with --verbosity diagnostic and report the issue");
    return (int)CliExitCode.InternalError;
}
