using OrientExpress.Spike002;

return args switch
{
    ["run-evidence", .. var rest] => EvidenceRunner.Run(rest),
    ["evaluate", .. var rest] => FolderEvaluator.Run(rest),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: spike002 run-evidence [--results <file>] [--tools-dir <dir>]");
    Console.Error.WriteLine("       spike002 evaluate --images <folder> [--max N] [--results <file>]  (read-only)");
    Console.Error.WriteLine("artifacts are digest-pinned; see Artifacts.cs for URLs and hashes.");
    return 2;
}
