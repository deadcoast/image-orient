using System.Reflection;
using OrientExpress.Cli.Hosting;
using OrientExpress.Cli.Output;
using OrientExpress.Contracts.Cli;

namespace OrientExpress.Cli.Commands;

public static class VersionCommand
{
    public static string ToolVersion { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    public static int Run(OutputContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Machine)
        {
            var document = new CommandResultDocument
            {
                Command = "version",
                ContractVersion = CliHost.ContractVersion,
                ToolVersion = ToolVersion,
                ExitCode = (int)CliExitCode.Success,
                ExitSymbol = CliExitCodeRegistry.SymbolOf(CliExitCode.Success),
                CorrelationId = context.CorrelationId,
            };
            if (context.Format == OutputFormat.Jsonl)
            {
                MachineWriter.WriteEventLine(context, MachineWriter.NewEvent(
                    context, "version", "version.reported", "succeeded"));
            }
            else
            {
                MachineWriter.WriteDocument(context, document);
            }
        }
        else
        {
            context.Out.WriteLine(
                $"{CliHost.ExecutableName} {ToolVersion} — contract {CliHost.ContractVersion}");
        }

        return (int)CliExitCode.Success;
    }
}
