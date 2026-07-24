# WP-017 context packet — job operations and integrity

Generated 2026-07-24 per DOC-DEVPLAN ("Work-package context packet").

## Objective

Operate on the jobs that scan/apply now create, without ever applying
image changes (PRD-FR-028): `jobs list`, `jobs show`, `jobs integrity`,
`jobs prune`, plus the standalone `upright verify <job>` re-check that
completes WP-013's command surface.

## Delivered

- `jobs list` — manifests under the configured job root, stable order.
- `jobs show <job>` — manifest, seal state/digest, plan counts by action,
  committed-row count from the event ledger.
- `jobs integrity [<job>]` — one job or all: SQLite integrity check, seal
  re-verification against recomputed row content, unresolved journal
  intents, and missing on-disk artifacts (manifest.json, plan.digest)
  (AC-049); any problem exits 32 VERIFICATION_FAILED.
- `jobs prune <job>` — dry-run by default; `--yes` removes; a job with
  unresolved journal intents refuses with JOB_RECOVERY_PENDING → 34
  CONFLICT (AC-050).
- `upright verify <job>` — re-verifies every committed output through the
  independent prober against the plan's recorded expectations, long after
  apply; corruption or deletion of an output exits 32 with per-row
  reasons. Read-only.
- Store: `PruneAsync` (dry-run + recovery-pending refusal); integrity
  gains missing-artifact findings.

## Acceptance criteria addressed

- AC-049 (partial): missing artifacts, digest mismatch (recomputed-content
  seal), and unresolved intents are detected; illegal state transitions
  are refused by schema triggers at write time; orphaned stage-file
  sweeps arrive with the journal work (WP-012).
- AC-050: dry-run default and recovery-pending refusal proven by test.

## Honest scope

- Retention windows and undo-retention holds (in-place mode) do not exist
  yet — prune's refusal rules cover journal recovery only until WP-012/015.
- `jobs show` counts stream the full plan; fine at current scales.
