using System.Globalization;
using OrientExpress.Spike001;

return args switch
{
    ["scan", "--root", var root, .. var rest] => Scan(root, rest),
    ["self-test", .. var rest] => SelfTest.Run(rest),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: spike001 scan --root <dir> [--results <file>] [--max-files N]");
    Console.Error.WriteLine("       spike001 self-test [--results <file>]");
    Console.Error.WriteLine("The scan is opt-in and read-only; output contains aggregates only.");
    return 2;
}

static int Scan(string root, string[] rest)
{
    var resultsPath = "spike001-inventory.json";
    var maxFiles = 1_000_000;
    for (var index = 0; index + 1 < rest.Length; index += 2)
    {
        switch (rest[index])
        {
            case "--results":
                resultsPath = rest[index + 1];
                break;
            case "--max-files":
                maxFiles = int.Parse(rest[index + 1], CultureInfo.InvariantCulture);
                break;
            default:
                return Usage();
        }
    }

    if (!Directory.Exists(root))
    {
        Console.Error.WriteLine($"spike001: root '{root}' does not exist.");
        return 2;
    }

    var report = InventoryScanner.Scan(root, maxFiles);
    InventoryScanner.Write(report, resultsPath);
    Console.WriteLine(
        $"spike001: probed {report.FilesProbed}/{report.FilesSeen} image files " +
        $"({report.Unreadable} unreadable); aggregates written to {Path.GetFullPath(resultsPath)}");
    return 0;
}
