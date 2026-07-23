using System.Globalization;
using System.Runtime.InteropServices;
using OrientExpress.Spike008;

return args switch
{
    ["run-evidence", .. var rest] => EvidenceRunner.Run(rest),
    ["crash-writer", .. var rest] => CrashWriter.Run(rest),
    _ => Usage(),
};

static int Usage()
{
    Console.Error.WriteLine("usage: spike008 run-evidence [--results <file>]");
    Console.Error.WriteLine("       spike008 crash-writer --db <path> --batches N --batch-size K --fault mid-transaction|after-commit");
    return 2;
}

/// <summary>Child mode: writes committed batches, then dies at the injected point without cleanup.</summary>
internal static partial class CrashWriter
{
    public static int Run(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < args.Length; index += 2)
        {
            options[args[index].TrimStart('-')] = args[index + 1];
        }

        var dbPath = options["db"];
        var batches = int.Parse(options["batches"], CultureInfo.InvariantCulture);
        var batchSize = int.Parse(options["batch-size"], CultureInfo.InvariantCulture);
        var fault = options["fault"];

        using var store = JobStore.Create(dbPath, "job-crash", "safe-copy");
        for (var batch = 0; batch < batches; batch++)
        {
            store.InsertFilesBatch(
                SyntheticRows.Generate(batchSize, startSeq: (long)batch * batchSize + 1),
                batchSize);
        }

        if (fault == "after-commit")
        {
            TerminateSelf();
        }

        // mid-transaction: open an explicit transaction, write uncommitted rows, die.
        store.ExecuteForTest("BEGIN IMMEDIATE");
        store.ExecuteForTest(
            "INSERT INTO files(job_id, seq, path_token, size, weak_fp) " +
            "SELECT job_id, seq + 10000000, path_token, size, weak_fp FROM files LIMIT 250");
        TerminateSelf();
        return 0;
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool TerminateProcess(nint process, uint exitCode);

    private static void TerminateSelf()
    {
        _ = TerminateProcess(GetCurrentProcess(), 42);
        Environment.FailFast("TerminateProcess returned unexpectedly.");
    }
}
