using System.Text;
using OrientExpress.Spike006.Protocol;

namespace OrientExpress.Spike006.Tests;

public sealed class CommitJournalTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("spike006-journal-").FullName;

    private static JournalRecord Record => new(
        Checkpoint: "None",
        JobId: "job-1",
        SourcePath: "source.bin",
        StagePath: "source.bin.oxstage-1",
        BackupPath: "backup.bin",
        OriginalSha256: "aa",
        ExpectedSha256: "bb");

    private CommitJournal CreateJournal()
        => new(Path.Combine(_directory, "journal.jsonl"));

    [Fact]
    public void Missing_journal_reads_as_no_checkpoint()
        => Assert.Equal(JournalCheckpoint.None, CreateJournal().ReadLastDurableCheckpoint());

    [Fact]
    public void Last_appended_checkpoint_wins()
    {
        var journal = CreateJournal();
        journal.Append(JournalCheckpoint.IntentDurable, Record);
        journal.Append(JournalCheckpoint.Committed, Record);

        Assert.Equal(JournalCheckpoint.Committed, journal.ReadLastDurableCheckpoint());
    }

    [Fact]
    public void Torn_trailing_line_is_ignored_and_prior_checkpoint_governs()
    {
        var journal = CreateJournal();
        journal.Append(JournalCheckpoint.IntentDurable, Record);
        File.AppendAllText(journal.Path, "{\"Checkpoint\":\"Comm", Encoding.UTF8);

        Assert.Equal(JournalCheckpoint.IntentDurable, journal.ReadLastDurableCheckpoint());
    }

    [Fact]
    public void Unknown_checkpoint_names_are_ignored()
    {
        var journal = CreateJournal();
        journal.Append(JournalCheckpoint.IntentDurable, Record);
        File.AppendAllText(
            journal.Path,
            "{\"Checkpoint\":\"Exploded\",\"JobId\":\"job-1\",\"SourcePath\":\"s\",\"StagePath\":\"t\",\"BackupPath\":\"b\",\"OriginalSha256\":\"aa\",\"ExpectedSha256\":\"bb\"}\n",
            Encoding.UTF8);

        Assert.Equal(JournalCheckpoint.IntentDurable, journal.ReadLastDurableCheckpoint());
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
