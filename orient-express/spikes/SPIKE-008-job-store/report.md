# SPIKE-008 report — job-store durability and scale

**Status:** complete — accepted under single-operator governance (2026-07-23)
**Run:** 2026-07-23, Windows 11 (build 10.0.26200), .NET 10.0.8, NTFS; Microsoft.Data.Sqlite 10.0.10, SQLite 3.53.3 (DEP-011)
**Raw evidence:** [`results/spike008-results.json`](results/spike008-results.json) · **Harness:** [`README.md`](README.md)

## Question and exit gate

Can a SQLite job store meet the DOC-DATAMODEL requirements — durable state
transitions, append-only events, sealed immutable plans, forward-only journal
checkpoints, guarded migrations — at 100k and 1m rows with crash recovery
(ADR-0008 proof gate)?

Targets declared before the run: batched insert ≥ 20k rows/sec; restart
open + state counts ≤ 2 s; v1→v2 migration ≤ 30 s; `integrity_check = ok` in
every scenario including post-crash; seal verifies after restart; crash
recovery preserves exactly the committed rows.

## Results — all targets met, with large margins

| Measurement | 100k rows | 1m rows | Target |
|---|---:|---:|---|
| Batched insert (files+plans, 5k/txn) | 162k rows/s | 185k rows/s | ≥ 20k |
| Event append (single txn) | 692k rows/s | 774k rows/s | — |
| Plan seal (incremental SHA-256) | 91 ms | 590 ms | — |
| Restart open + counts | 7 ms | 43 ms | ≤ 2000 ms |
| Pending-batch query (indexed, 500) | <1 ms | <1 ms | — |
| Migration v1→v2 (add column + index) | 24 ms | 243 ms | ≤ 30 s |
| `integrity_check` | 106 ms | 1127 ms | ok |
| Database size | 25 MB | 185 MB | — |

**Crash matrix:** kernel termination mid-transaction and after commit both
recover with `integrity_check = ok` and exactly the committed rows (10,000)
present — the uncommitted transaction vanishes entirely. WAL recovery needs
no manual procedure beyond reopening the database.

**Constraints hold in the database, not just the application.** Triggers
enforce append-only events, sealed-plan immutability (including INSERT), and
forward-only journal checkpoints; 9 CI tests prove each rule and that a
tampered plan row is caught by seal re-verification even if the protecting
trigger is removed.

**Batching economics:** single-row transactions reached only 21k rows/s —
6.5× slower than 1k-row batches — confirming the DOC-DATAMODEL bounded
persistence queue with batched writes as a hard requirement, not an
optimization.

## Dependency-intake finding (workstream E)

NuGet audit **failed the build** on first restore: Microsoft.Data.Sqlite
10.0.10 transitively pins `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, which carries
high-severity advisory GHSA-2m69-gcr7-jv3q. Resolved by directly pinning
`SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 (DEP-018). WP-009 must keep the bundle
pin under NFR-SEC-002 review whenever DEP-011 is bumped.

## Limitations

- Single-writer local NTFS only; network filesystems and WAL-unsafe volumes
  remain policy-refused per DOC-DATAMODEL (untested here by design).
- Crash injection is process-level kernel termination, not OS crash or power
  loss; `synchronous=NORMAL` under real power failure relies on WAL semantics
  documented by SQLite and should be revisited in WP-021 soak testing.
- Concurrency was not exercised beyond the single-logical-writer design; the
  bounded persistence queue lands in WP-011.
- The v1→v2 migration is additive; a table-rebuild migration at 1m rows was
  not measured.

## Recommendation

Confirm ADR-0008: SQLite via Microsoft.Data.Sqlite is the job store.
Carry into WP-009: WAL + `synchronous=NORMAL` + explicit truncate
checkpoints; batched writes (≥1k rows/txn) through the single-writer queue;
constraints as triggers in the schema, not only application checks;
incremental canonical seal digests; refusal of out-of-range schema versions.
Alternatives re-examined: loose JSON files and embedded document stores
remain rejected per ADR-0008; nothing in the evidence weakens that.

## Affected items

- **ADR-0008 0.1.1** — provisional → accepted; proof gate met.
- **DEP-011** — selected; scale evidence recorded. **DEP-018** added
  (SQLitePCLRaw bundle pin, advisory-driven).
- **DOC-WBS** — design notes for WP-004, WP-009, WP-011, WP-017.
- **planning/spike-register.csv** — SPIKE-008 → complete.

## Spike completion checklist (DOC-SPIKES)

- [x] Falsifiable question and pass/fail thresholds declared before the run.
- [x] Inputs/fixtures documented — synthetic sequence-derived rows, no user data.
- [x] Harness repeatable from a clean checkout (`README.md`).
- [x] Raw output machine-readable (`results/spike008-results.json`).
- [x] Failure cases included (crash matrix, tamper, future-version refusal, checkpoint regression).
- [x] Recommendation compares credible alternatives (ADR-0008 set re-examined).
- [x] ADR and dependency register updated — ADR-0008 0.1.1; DEP-011/DEP-018.
- [x] Follow-on work package plans updated — DOC-WBS design notes.
- [x] Artifact retained in-repo under the evidence policy (small, non-private).
