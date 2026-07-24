using OrientExpress.Spike007;

return args switch
{
    ["run-evidence", .. var rest] => EvidenceRunner.Run(rest),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: spike007 run-evidence [--results <file>] [--exiftool <path>]");
    return 2;
}
