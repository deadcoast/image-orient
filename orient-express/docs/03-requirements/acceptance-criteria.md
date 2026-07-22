---
document_id: DOC-AC
title: Acceptance Criteria
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Quality
depends_on: [DOC-PRD, DOC-NFR]
traces_to: [AC-001, AC-002, AC-003, AC-004, AC-005, AC-006, AC-007, AC-008, AC-009, AC-010, AC-011, AC-012, AC-013, AC-014, AC-015, AC-016, AC-017, AC-018, AC-019, AC-020, AC-021, AC-022, AC-023, AC-024, AC-025, AC-026, AC-027, AC-028, AC-029, AC-030, AC-031, AC-032, AC-033, AC-034, AC-035, AC-036, AC-037, AC-038, AC-039, AC-040, AC-041, AC-042, AC-043, AC-044, AC-045, AC-046, AC-047, AC-048, AC-049, AC-050]
---

# Acceptance criteria

Each criterion is observable and mapped to at least one test/evidence row. Passing an isolated criterion does not waive system invariants or non-functional gates.

| ID | Requirement | Criterion |
| --- | --- | --- |
| AC-001 | PRD-FR-001 | Given mixed files/directories and filters, discovery returns the same canonical ordered set across repeated runs. |
| AC-002 | PRD-FR-001 | Loops, inaccessible paths, reparse points, aliases, and duplicate identities are handled according to policy without infinite traversal. |
| AC-003 | PRD-FR-002 | `doctor --json` emits a schema-valid capability report and performs no source writes. |
| AC-004 | PRD-FR-002 | `inspect` explains stored dimensions, EXIF transform, candidate evidence, proposed action, and blockers for one file. |
| AC-005 | PRD-FR-003 | Golden images for all EXIF Orientation values 1–8 render to the same intended upright view. |
| AC-006 | PRD-FR-004 | Candidate generation composes EXIF display transforms before additional rotational hypotheses. |
| AC-007 | PRD-FR-004 | Candidate scoring is deterministic for a pinned model, runtime, policy, and input. |
| AC-008 | PRD-FR-005 | Face detections expose bounding boxes, confidence, and five landmarks for each candidate. |
| AC-009 | PRD-FR-005 | No persisted artifact contains an identity embedding or cross-file face identifier. |
| AC-010 | PRD-FR-006 | Automation requires configured score, top-two margin, and minimum-evidence gates. |
| AC-011 | PRD-FR-006 | Ambiguous, no-face, contradictory multi-face, or out-of-distribution cases become REVIEW rather than forced rotations. |
| AC-012 | PRD-FR-007 | A job plan validates against its schema and records tool, policy, model, runtime, and source fingerprints. |
| AC-013 | PRD-FR-007 | Once sealed, plan rows cannot be edited through normal commands; review overrides are append-only decisions. |
| AC-014 | PRD-FR-008 | `scan` creates no image output and does not modify source timestamps, data streams, or metadata. |
| AC-015 | PRD-FR-008 | `apply` refuses a source changed after scan and emits a stable stale-plan result. |
| AC-016 | PRD-FR-009 | Safe-copy mode preserves relative paths and never overwrites an unrelated existing destination. |
| AC-017 | PRD-FR-009 | Collision behavior is deterministic, logged, and configurable as error, skip, or content-aware rename. |
| AC-018 | PRD-FR-010 | In-place mode cannot begin without explicit acknowledgement and a writable verified backup target. |
| AC-019 | PRD-FR-010 | Fault injection before atomic replacement leaves the source unchanged and a recoverable journal. |
| AC-020 | PRD-FR-010 | Fault injection after replacement but before final checkpoint is reconciled safely on restart. |
| AC-021 | PRD-FR-011 | Eligible JPEG rotations use the DCT-domain path without generation loss. |
| AC-022 | PRD-FR-011 | Non-MCU-aligned JPEG behavior follows the selected abort/reencode/trim/preserve-partial policy and is never silent. |
| AC-023 | PRD-FR-012 | Successful normalization writes display-equivalent upright pixels and sets orientation metadata to 1. |
| AC-024 | PRD-FR-012 | Required metadata fields pass the device/format round-trip corpus under the active preservation policy. |
| AC-025 | PRD-FR-013 | Verification reopens output and rejects decode failure, wrong dimensions, wrong orientation, or missing required metadata. |
| AC-026 | PRD-FR-013 | A verification failure prevents commit or triggers recovery without deleting the last known-good source. |
| AC-027 | PRD-FR-014 | Cancellation produces a resumable state and a documented partial-completion exit code. |
| AC-028 | PRD-FR-014 | Replaying a completed operation does not duplicate output or corrupt journal state. |
| AC-029 | PRD-FR-015 | Undo restores a byte-verified backup when the current file matches the expected applied state. |
| AC-030 | PRD-FR-015 | Undo refuses and reports conflict when the current file was independently changed. |
| AC-031 | PRD-FR-016 | Review decisions can be exported, edited with documented values, imported, validated, and appended to the job. |
| AC-032 | PRD-FR-016 | Review thumbnails are orientation-labeled, bounded in size, and stored only when explicitly enabled. |
| AC-033 | PRD-FR-017 | `--json` emits one schema-valid document; `--jsonl` emits independently parseable event lines. |
| AC-034 | PRD-FR-017 | With machine output selected, stdout contains no progress bars, ANSI styling, or unsolicited prose. |
| AC-035 | PRD-FR-018 | All documented result classes map to stable exit codes tested on Windows PowerShell and cmd.exe. |
| AC-036 | PRD-FR-019 | Configuration precedence is CLI > environment > explicit file > user file > defaults. |
| AC-037 | PRD-FR-019 | Unknown or invalid configuration fields fail before file processing unless forward-compatible mode is explicitly selected. |
| AC-038 | PRD-FR-020 | A model bundle with wrong digest, unsupported schema, or incompatible runtime is quarantined and never activated. |
| AC-039 | PRD-FR-020 | Model activation is atomic and rollback restores the prior verified model. |
| AC-040 | PRD-FR-021 | Format support is reported per operation; unsupported write/metadata combinations fail during planning rather than mid-commit. |
| AC-041 | PRD-FR-022 | Configured queue and worker limits bound memory under a 100,000-file synthetic workload. |
| AC-042 | PRD-FR-022 | Ctrl+C first requests graceful cancellation; a second interrupt terminates promptly with durable state. |
| AC-043 | PRD-FR-023 | Every file has a terminal event with action, result, reason code, timings, and provenance. |
| AC-044 | PRD-FR-023 | Default logs contain no image bytes and path redaction is available for support bundles. |
| AC-045 | PRD-FR-024 | Sequence consensus can break a bounded tie but cannot override hard safety or evidence gates. |
| AC-046 | PRD-FR-025 | Scene fallback remains disabled unless its evaluation report demonstrates statistically supported net benefit. |
| AC-047 | PRD-FR-026 | Benchmark output records hardware, OS, runtime, model, configuration, corpus descriptor, throughput, latency, and memory. |
| AC-048 | PRD-FR-027 | Generated PowerShell completion and UTF-8 response files handle spaces, Unicode, and long paths. |
| AC-049 | PRD-FR-028 | Job integrity detects missing artifacts, invalid schema, digest mismatch, illegal state transition, and orphaned temporary files. |
| AC-050 | PRD-FR-028 | Prune defaults to dry-run and cannot remove jobs with pending recovery or undo retention. |

The canonical test mapping is [`planning/test-matrix.csv`](../../planning/test-matrix.csv).
