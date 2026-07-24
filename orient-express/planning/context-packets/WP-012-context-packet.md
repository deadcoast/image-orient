# WP-012 context packet — journal, commit, recovery, and undo

Generated 2026-07-24 per DOC-DEVPLAN ("Work-package context packet").

## Objective

Guarded in-place mode (PRD-FR-010) and undo (PRD-FR-015) on the
SPIKE-006-qualified protocol: verified backup -> durable journal intent ->
same-directory stage -> independent verification -> write-through
ReplaceFileW -> post-replace verification -> commit; reconcile-first on
every later run; undo restores byte-verified backups only where reality
still matches the journal.

## Delivered

- Store journal API: `OpenIntentAsync` (Prepared), `AdvanceIntentAsync`
  (forward-only — the schema trigger refuses regression with
  JOURNAL_CHECKPOINT_REGRESSION), `ReadIntentsAsync`; unresolved intents
  already block prune (AC-050).
- `scan --mode in-place --backup-root <dir>`: strong fingerprints forced
  (full SHA-256 before destructive apply); the manifest's output root is
  the backup root for in-place jobs.
- `apply` for in-place jobs: refuses without `--acknowledge-in-place`
  (31), refuses non-NTFS-fixed volumes (NFR-COMP-001, 31), reconciles the
  journal FIRST (unreconcilable state exits 33 RECOVERY_REQUIRED and
  nothing new runs). Per row: backup copied and byte-verified against the
  scanned fingerprint (AC-018), intent durable before the swap, transform
  + independent verification on the stage, ReplaceFileW, post-replace
  digest check, commit + Reconciled. Every abort path closes its intent
  or deliberately leaves it at Replaced for reconcile.
- Reconcile: crash before Replaced -> source must match its pre-state
  (stage deleted, intent closed, event recorded); crash after Replaced ->
  source must match the staged post-state (commit completed on restart);
  anything else stops with 33 (AC-019/AC-020 semantics).
- `undo <job> --yes`: for each committed replacement — current file must
  match the journal's post-apply digest (else UNDO_STATE_MISMATCH
  conflict, file untouched, AC-030) and the backup must match the
  pre-apply digest; restore is itself journaled (fresh undo-N intents;
  checkpoints stay forward-only) and idempotent (already-undone rows are
  counted, not re-restored). Exits 0 / 11 partial / 34 conflict / 12
  nothing.

## Honest scope

- AC-019/AC-020 are implemented as protocol + reconcile logic with
  crash-point semantics proven at the unit/e2e level; the systematic
  kernel-kill fault-injection MATRIX across compatibility classes is
  WP-015's gate, so in-place stays labeled guarded-alpha until then.
- Retention holds: backups are never deleted by the tool; prune refuses
  recovery-pending jobs; richer retention windows arrive with WP-015.
- ADS re-attachment on replaced files (SPIKE-004 finding) is not yet
  wired; the journal records enough to add it in WP-015 hardening.
