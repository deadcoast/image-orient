using OrientExpress.Spike005;

return args switch
{
    ["run-evidence", .. var rest] => EvidenceRunner.Run(rest),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: spike005 run-evidence [--results <file>] [--tools-dir <dir>]");
    return 2;
}
