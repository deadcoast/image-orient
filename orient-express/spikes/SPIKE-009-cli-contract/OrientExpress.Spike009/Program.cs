using System.Text;
using OrientExpress.Spike009;
using OrientExpress.Spike009.Evidence;

// Machine output must be UTF-8 without BOM even when redirected: on Windows a
// redirected console otherwise defaults to the legacy ANSI/OEM codepage.
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

if (args is ["run-evidence", .. var rest])
{
    return EvidenceRunner.Run(rest);
}

return CliHost.Invoke(args, Console.Out, Console.Error);
