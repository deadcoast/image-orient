---
document_id: DOC-WBS
title: Work Breakdown Structure and Backlog Dependencies
version: 0.1.4
status: baseline
as_of: 2026-07-23
owner: Program
depends_on: [DOC-SPIKES, DOC-ARCH, DOC-SRS]
traces_to: [WP-001, WP-002, WP-003, WP-004, WP-005, WP-006, WP-007, WP-008, WP-009, WP-010, WP-011, WP-012, WP-013, WP-014, WP-015, WP-016, WP-017, WP-018, WP-019, WP-020, WP-021, WP-022, WP-023, WP-024]
---

# Work breakdown structure

Work packages are vertical outcomes, not folders or generic engineering activities. A package may start only when every listed spike/WP dependency has passed or an approved deviation records the risk.

| ID | Milestone | Work package | Depends on | Outputs | Requirements | Components |
| --- | --- | --- | --- | --- | --- | --- |
| WP-001 | M0 | Repository and engineering baseline | — | Solution layout; analyzers; formatting; deterministic builds; CI skeleton; documentation validator | NFR-MAINT-001;NFR-SEC-002 | — |
| WP-002 | M1 | CLI host and contracts | SPIKE-009;WP-001 | Command tree; help; cancellation; exit codes; JSON/JSONL and human renderers; completion | PRD-FR-002;PRD-FR-017;PRD-FR-018;PRD-FR-027 | COMP-CLI;COMP-REPORTING |
| WP-003 | M1 | Configuration and schemas | WP-001 | Configuration precedence; profiles; validation; effective config; schemas | PRD-FR-019;NFR-MAINT-002 | COMP-CONFIG |
| WP-004 | M1 | Filesystem and identity foundation | SPIKE-006;WP-001 | Path canonicalization; file IDs; fingerprinting; locks; temp placement; atomic primitives; placeholder/reparse policy | PRD-FR-001;PRD-FR-010;NFR-COMP-001;NFR-COMP-002 | COMP-FILESYSTEM;COMP-NATIVE |
| WP-005 | M1 | Metadata and codec adapters | SPIKE-004;SPIKE-007;WP-004 | ExifTool adapter; bounded decode; format probes; metadata diff/preservation policy | PRD-FR-003;PRD-FR-012;PRD-FR-021;NFR-FID-001 | COMP-METADATA;COMP-CODEC;COMP-NATIVE |
| WP-006 | M1 | Model registry and inference runtime | SPIKE-003;WP-001;WP-004 | Model manifests; digest verification; runtime abstraction; provider fallback; activation/rollback | PRD-FR-020;NFR-SEC-002;NFR-PRIV-001 | COMP-MODELS;COMP-INFERENCE |
| WP-007 | M2 | Orientation feature engine | SPIKE-001;SPIKE-002;WP-005;WP-006 | Four candidates; face aggregation; geometric features; evidence trace | PRD-FR-004;PRD-FR-005 | COMP-ORIENTATION;COMP-FEATURES |
| WP-008 | M2 | Calibration and decision policy | SPIKE-001;SPIKE-002;WP-007 | Calibration training/evaluation; thresholds; abstention; metadata prior; reason codes | PRD-FR-006;PRD-FR-024;PRD-FR-025;NFR-ML-001;NFR-ML-002;NFR-ML-003;NFR-ML-004 | COMP-CALIBRATION;COMP-POLICY |
| WP-009 | M2 | Job store and immutable planning | SPIKE-008;WP-003;WP-004;WP-007;WP-008 | SQLite schema; migrations; manifest; append-only events; sealed plans; stale-input checks | PRD-FR-007;PRD-FR-008;PRD-FR-014;PRD-FR-028 | COMP-JOBSTORE;COMP-PLANNER |
| WP-010 | M3 | Transform engine | SPIKE-004;SPIKE-005;SPIKE-007;WP-005 | Transform composition; lossless JPEG path; raster path; orientation normalization; metadata write | PRD-FR-011;PRD-FR-012;NFR-FID-002 | COMP-TRANSFORM;COMP-CODEC;COMP-METADATA |
| WP-011 | M3 | Bounded execution pipeline | SPIKE-006;SPIKE-008;WP-009;WP-010 | Bounded channels; resource governors; cancellation; retry taxonomy; resumable apply | PRD-FR-014;PRD-FR-022;NFR-PERF-001;NFR-PERF-002;NFR-PERF-003 | COMP-PIPELINE;COMP-EXECUTOR |
| WP-012 | M3 | Journal, commit, recovery, and undo | SPIKE-006;WP-004;WP-009;WP-011 | Write-ahead journal; verified backups; atomic commit; reconciliation; undo conflicts | PRD-FR-010;PRD-FR-014;PRD-FR-015;NFR-REL-001;NFR-REL-002;NFR-REL-004 | COMP-JOURNAL;COMP-EXECUTOR;COMP-FILESYSTEM |
| WP-013 | M3 | Independent verification | WP-005;WP-009;WP-010 | Independent reopen; expected-state comparison; verification taxonomy; quarantine | PRD-FR-013 | COMP-VERIFY |
| WP-014 | M3 | Safe-copy end-to-end mode | WP-009;WP-010;WP-011;WP-013 | Destination planning; collision handling; full scan/apply/verify workflow; reports | PRD-FR-009;PRD-FR-023 | COMP-PLANNER;COMP-EXECUTOR;COMP-REPORTING |
| WP-015 | M4 | Guarded in-place end-to-end mode | WP-012;WP-013;WP-014 | Explicit acknowledgement; backup retention; atomic replace; recovery; undo | PRD-FR-010;PRD-FR-015 | COMP-EXECUTOR;COMP-JOURNAL |
| WP-016 | M4 | Review workflow | WP-002;WP-007;WP-008;WP-009 | Review queue; show/list; thumbnail policy; export/import; override events | PRD-FR-016 | COMP-REVIEW |
| WP-017 | M4 | Job operations and integrity | WP-002;WP-009;WP-012 | jobs list/show/prune/integrity; recovery diagnostics; retention controls | PRD-FR-028;NFR-OBS-001;NFR-OBS-002 | COMP-JOBSTORE;COMP-JOURNAL;COMP-REPORTING |
| WP-018 | M4 | Operational UX and reporting | WP-002;WP-014;WP-016;WP-017 | Human summaries; machine events; diagnostics bundle; path redaction; support playbooks | PRD-FR-017;PRD-FR-018;PRD-FR-023;NFR-UX-001;NFR-UX-002 | COMP-CLI;COMP-REPORTING |
| WP-019 | M5 | Security and parser hardening | WP-004;WP-005;WP-006;WP-009;WP-010 | Threat controls; fuzzing; timeouts; decompression limits; malicious model/config/job tests | NFR-SEC-001;NFR-SEC-003;NFR-PRIV-001;NFR-PRIV-002;NFR-PRIV-003 | COMP-NATIVE;COMP-CODEC;COMP-MODELS;COMP-JOBSTORE |
| WP-020 | M5 | Quality corpus and release calibration | SPIKE-001;WP-008;WP-014;WP-015 | Governed corpus; slice metrics; confidence intervals; regression gates; model card | NFR-ML-001;NFR-ML-002;NFR-ML-003;NFR-ML-004 | COMP-CALIBRATION;COMP-POLICY |
| WP-021 | M5 | Performance and soak qualification | WP-011;WP-014;WP-017 | Microbenchmarks; 100k soak; cancellation/restart; memory/throughput gates; benchmark command | PRD-FR-022;PRD-FR-026;NFR-PERF-001;NFR-PERF-002;NFR-PERF-003;NFR-PERF-004 | COMP-BENCHMARK;COMP-PIPELINE |
| WP-022 | M6 | Portable distribution | SPIKE-003;SPIKE-007;SPIKE-010;WP-018;WP-019;WP-021 | Self-contained win-x64 zip; native/model inventory; clean-VM qualification; update/rollback docs | NFR-COMP-001;NFR-SEC-002 | COMP-CAPABILITIES;COMP-MODELS |
| WP-023 | M6 | Installer, signing, SBOM, and provenance | SPIKE-010;WP-022 | Signed installer; uninstaller; SBOM; checksums; provenance attestations; license notices | NFR-SEC-002;NFR-MAINT-002 | — |
| WP-024 | M7 | Release candidate and operational handoff | WP-018;WP-019;WP-020;WP-021;WP-023 | RC gates; compatibility report; support/runbooks; rollback drill; release notes | GOAL-001;GOAL-002;GOAL-003;GOAL-004;GOAL-005;GOAL-006;GOAL-007;GOAL-008 | — |

## Spike-driven design notes

- **WP-004 (from SPIKE-006):** the NTFS file ID does not survive `ReplaceFileW`;
  identity components must re-baseline file identity after every in-place
  commit and must not key plans or journals on the file ID across a commit.
  Sharing-violation (32) and access-denied (5) are actionable stop/retry
  classes; read-only sources need an explicit precondition before in-place is
  offered. See the [SPIKE-006 report](../../spikes/SPIKE-006-atomic-replace/report.md).
- **WP-011 / WP-012 / WP-015 (from SPIKE-006):** the journal checkpoint set
  (`IntentDurable`, `Committed`, `Finalized`), torn-line tolerance, and the
  recovery decision table are proven on NTFS and should be carried into the
  production journal design unchanged; `MoveFileEx` replace is disqualified
  for in-place commits.
- **WP-002 (from SPIKE-009):** the host must reject option-like tokens bound
  into greedy positional arguments; machine output requires explicit
  UTF-8-without-BOM console encoding; response files are host-expanded (one
  argument per line); grammar errors keep machine stdout empty. Registry
  numerics freeze at WP-002 entry. See the
  [SPIKE-009 report](../../spikes/SPIKE-009-cli-contract/report.md).
- **WP-005 / WP-010 / WP-014 (from SPIKE-004):** the metadata adapter is the
  typed `-stay_open` protocol with per-command `{ready<id>}` **and** `-echo4`
  stderr sentinels (diagnostics must be attributable); reads use
  `-j -G1 -a -n`; writes use `-P -overwrite_original` on the stage copy.
  ExifTool's rewrite drops ADS and changes the file id — both fold into the
  commit protocol's re-baseline, and ADS re-attachment is the executor's
  responsibility, never the adapter's. Maker-note guarantees stay gated on
  the corpus tranche. See the
  [SPIKE-004 report](../../spikes/SPIKE-004-metadata/report.md).
- **WP-010 / WP-013 / WP-014 (from SPIKE-005):** `jpegEdge` policy mapping is
  approved — `abort` = `jpegtran -perfect` with `JPEG_EDGE_NOT_PERFECT`
  refusal; `preserve-partial` = default mode (reversible, imperfect edges,
  disclosed); `trim` = `-trim` (visible resize); `reencode` = raster path.
  Always invoke with `-copy all` and re-apply metadata via the metadata
  adapter. **WP-013 verifier must prove losslessness by inverse-transform
  round-trip or coefficient digest — never by decoded-pixel comparison
  against a spatial reference** (integer IDCT/chroma upsampling are not
  rotation-equivariant; decode deltas ≤4 are expected). See the
  [SPIKE-005 report](../../spikes/SPIKE-005-jpeg-edge/report.md).
- **WP-004 / WP-009 / WP-011 / WP-017 (from SPIKE-008):** batched writes
  through the single-writer queue are a requirement (single-row transactions
  are 6.5× slower); state-transition constraints live in the schema as
  triggers; WAL + `synchronous=NORMAL` + explicit truncate checkpoints is the
  qualified durability policy; out-of-range schema versions are refused. Keep
  the SQLitePCLRaw bundle pin (DEP-018) under review on every DEP-011 bump.
  See the [SPIKE-008 report](../../spikes/SPIKE-008-job-store/report.md).

## Sizing and decomposition rule

Each WP is decomposed into implementation stories no larger than one independently reviewable behavior. Stories retain the parent WP and requirement/AC/test IDs. Do not create horizontal “all repositories/interfaces first” work that delays a vertical proof.

## Critical integration increments

### Increment 1 — Diagnostic shell

`doctor` and `inspect` use real capability, metadata, decode, and model adapters but perform no writes.

### Increment 2 — Sealed scan plan

`scan` processes real folders into SQLite and emits explainable JSON/JSONL, with REVIEW/UNSUPPORTED behavior.

### Increment 3 — Safe-copy JPEG/PNG

`apply` transforms planned actions, preserves metadata under the baseline profile, verifies, resumes, and reports.

### Increment 4 — Recovery and review

Review decisions, job integrity, fault recovery, and undo are complete.

### Increment 5 — Release hardening

Corpus gates, adversarial/fuzz, compatibility, soak, package/signing, update/rollback, and support handoff.

## Backlog policy

- Every implementation story has an executable acceptance test or named evidence task.
- Bugs affecting source safety, wrong automatic rotation, metadata loss, schema incompatibility, or recovery are release blockers until triaged through the risk process.
- Refactoring is tied to a violated architecture/quality metric, not an unbounded cleanup epic.
- Deferred features remain outside Must requirements and do not delay v1.
