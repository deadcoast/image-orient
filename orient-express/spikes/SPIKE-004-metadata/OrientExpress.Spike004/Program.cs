using OrientExpress.Spike004;

return args switch
{
    ["run-evidence", .. var rest] => EvidenceRunner.Run(rest),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: spike004 run-evidence [--results <file>] [--exiftool <path>] [--cjpeg <path>]");
    return 2;
}
