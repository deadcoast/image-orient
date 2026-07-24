# WP-009 context packet — job store and immutable planning

Generated 2026-07-24 per DOC-DEVPLAN ("Work-package context packet").

## Objective

The durable memory of a job: SQLite-canonical state per DOC-DATAMODEL with
the SPIKE-008-proven settings (WAL + synchronous NORMAL + batched
single-writer transactions + schema triggers as the constraint of record),
immutable manifests, write-once content-addressed plan seals, append-only
events, and stale-input drift detection (PRD-FR-007, PRD-FR-008,
PRD-FR-014, PRD-FR-028, NFR-REL-003).

## Sequencing deviation (recorded under single-operator governance)

The WBS lists WP-007/WP-008 (orientation engine and calibrated policy)
among WP-009's dependencies because plans record model/policy provenance.
Those packages remain gated on corpus evidence. WP-009 proceeds now because
the store's schema carries provenance as nullable identities: the
deterministic EXIF-normalization path (WP-010) has NO model, and a null
`model_identity` is the honest record of that. When WP-007/008 land, their
identities populate the same columns — no schema change.

## Delivered

- `OrientExpress.Contracts.Jobs`: `JobId`, `JobManifest` (immutable
  `manifest.json` shape; model/policy identity nullable), `JobHandle`,
  `PlanAction` (Keep/Normalize/Review/Unsupported), `PlannedFile` (identity
  fields recorded for stale-input refusal + the WP-010 transform fields),
  `PlanSeal`, `PlanQuery`, `JobEvent`, `IntegrityReport`.
- `OrientExpress.Application.Jobs`: `IJobStore` port (DOC-COMPONENTS
  shape); `StaleInputCheck` — pure drift detection (size, last-write,
  content prefix/full, volume, file id) with named reasons, plus the
  DOC-DATAMODEL fingerprint-strength rule (a weaker fingerprint cannot
  satisfy a stronger policy).
- `OrientExpress.Infrastructure.Sqlite`: `SqliteJobStore` — one directory
  per job (`manifest.json` written once, `job.db` canonical, `plan.digest`
  human echo), schema v1 with triggers enforcing append-only events,
  sealed-plan immutability, write-once seals, and forward-only journal
  checkpoints; batched scan upserts; streaming canonical-order plan reads;
  `jobs` integrity backing (PRAGMA integrity_check + seal verify +
  unresolved-intent count). The seal digests recomputed row CONTENT, never
  the stored row digest — a tampered row cannot hide behind a stale digest
  column (proven by test).
- Doctor: `job-store` reports implemented.

## Error semantics

`JOB_NOT_FOUND`, `JOB_ALREADY_EXISTS`, `JOB_MANIFEST_INVALID`,
`JOB_SCHEMA_UNSUPPORTED` (a newer version is diagnosed, never guessed),
`PLAN_SEALED`, `PLAN_ALREADY_SEALED`, `PLAN_SEAL_MISMATCH`.

## Acceptance criteria addressed

- AC-012 (partial): plans record tool/config/source fingerprints and
  nullable model/policy identity; schema validation of exported plans
  arrives with the export contract.
- AC-013: sealed rows refuse mutation in the schema itself; review
  overlays (WP-016) are append-only by the same trigger discipline.
- AC-015 (store side): `StaleInputCheck` yields the stable drift reasons
  apply will refuse with; the refusal wiring itself is WP-011/WP-012.

## Honest scope

- No CLI surface yet: `jobs list/show/prune/integrity` are WP-017; `scan`
  and `apply` still refuse — they connect through WP-011..WP-014.
- The `assessments` and `review_decisions` tables arrive with WP-007/008
  and WP-016 as forward-only migrations; the migration guard
  (`schema_migrations`, version fencing) is in place.
- Crash-fault matrices at scale were proven in SPIKE-008; this package's
  tests cover the production store's contract behavior, not a re-run of
  the 1m-row benchmarks.
