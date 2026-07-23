using System.Text;
using System.Text.Json;

namespace OrientExpress.Spike006.Protocol;

/// <summary>
/// Durable commit checkpoints from DOC-FSSAFE. Order matters: recovery keys off
/// the last checkpoint that reached stable storage before the crash.
/// </summary>
public enum JournalCheckpoint
{
    None = 0,
    IntentDurable = 1,
    Committed = 2,
    Finalized = 3,
}

public sealed record JournalRecord(
    string Checkpoint,
    string JobId,
    string SourcePath,
    string StagePath,
    string BackupPath,
    string OriginalSha256,
    string ExpectedSha256);

/// <summary>
/// Append-only JSON-lines journal written with write-through and an explicit
/// flush so each checkpoint is durable before the protocol continues. A torn
/// final line (crash mid-append) must be ignored, never treated as corruption.
/// </summary>
public sealed class CommitJournal(string path)
{
    public string Path { get; } = path;

    public void Append(JournalCheckpoint checkpoint, JournalRecord record)
    {
        var line = JsonSerializer.Serialize(record with { Checkpoint = checkpoint.ToString() });
        using var stream = new FileStream(
            Path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        stream.Write(Encoding.UTF8.GetBytes(line + "\n"));
        stream.Flush(flushToDisk: true);
    }

    public JournalCheckpoint ReadLastDurableCheckpoint()
    {
        var last = JournalCheckpoint.None;
        if (!File.Exists(Path))
        {
            return last;
        }

        foreach (var line in File.ReadLines(Path))
        {
            if (TryParseCheckpoint(line, out var checkpoint))
            {
                last = checkpoint;
            }
        }

        return last;
    }

    private static bool TryParseCheckpoint(string line, out JournalCheckpoint checkpoint)
    {
        checkpoint = JournalCheckpoint.None;
        try
        {
            var record = JsonSerializer.Deserialize<JournalRecord>(line);
            return record is not null
                && Enum.TryParse(record.Checkpoint, ignoreCase: false, out checkpoint);
        }
        catch (JsonException)
        {
            // A torn trailing line is expected crash debris; earlier durable lines govern.
            return false;
        }
    }
}
