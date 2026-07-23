# SPIKE-008 harness — job-store durability and scale

Decision harness for [SPIKE-008](../../docs/06-delivery/technical-spikes.md#spike-008--job-store-durability-and-scale):
qualifies the DOC-DATAMODEL job store on SQLite via Microsoft.Data.Sqlite
(DEP-011, ADR-0008). Evidence tooling, not the WP-009 job store.

## What it does

- **Schema prototype** — `jobs`, `files`, `plans`, `events`, `journal`, and
  `schema_migrations` with the DOC-DATAMODEL state-transition constraints
  enforced in the database itself: append-only `events` (UPDATE/DELETE raise),
  sealed-plan immutability (INSERT/UPDATE/DELETE on `plans` raise once the
  job carries a plan seal), and forward-only journal checkpoints
  (`Prepared → StagedVerified → IntentDurable → Replaced → Committed →
  Reconciled`; regression or repeat raises).
- **Durability policy** — WAL journal, `synchronous=NORMAL`, foreign keys on,
  explicit `wal_checkpoint(TRUNCATE)`; a schema version outside the supported
  range is refused, never guessed.
- **Plan sealing** — canonical ordered serialization digested incrementally
  (scales to 1m rows); `VerifySeal` re-derives and detects any row mutation.
- **Crash matrix** — a child process is kernel-terminated mid-transaction and
  after commit; the parent reopens and requires `integrity_check = ok` with
  exactly the committed rows surviving.
- **ADR-0008 scale gate** — 100k **and** 1m synthetic rows with falsifiable
  targets declared in the report: batched insert ≥ 20k rows/sec, restart
  open+counts ≤ 2 s, v1→v2 migration ≤ 30 s, integrity ok, seal verifies
  after restart.
- **Constraint tests** — 9 unit tests run in CI against small databases.

Synthetic rows derive from sequence numbers; no user data is involved.

## Reproduce

```powershell
dotnet build OrientExpress.slnx -c Release
./spikes/SPIKE-008-job-store/OrientExpress.Spike008/bin/Release/net10.0/spike008.exe `
  run-evidence --results spikes/SPIKE-008-job-store/results/spike008-results.json
```

Exit code 0 means every scenario passed its targets. Raw results:
[`results/spike008-results.json`](results/spike008-results.json). Findings and
recommendation: [`report.md`](report.md).
