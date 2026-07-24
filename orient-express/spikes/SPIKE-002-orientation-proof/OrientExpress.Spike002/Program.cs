using OrientExpress.Spike002;

return args switch
{
    ["run-evidence", .. var rest] => EvidenceRunner.Run(rest),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: spike002 run-evidence [--results <file>] [--tools-dir <dir>]");
    Console.Error.WriteLine("artifacts are digest-pinned; see Artifacts.cs for URLs and hashes.");
    return 2;
}
