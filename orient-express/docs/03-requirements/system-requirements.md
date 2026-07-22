---
document_id: DOC-SRS
title: System Requirements Specification
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-PRD, DOC-NFR, DOC-AC]
traces_to: [PRD-FR-001, PRD-FR-002, PRD-FR-003, PRD-FR-004, PRD-FR-005, PRD-FR-006, PRD-FR-007, PRD-FR-008, PRD-FR-009, PRD-FR-010, PRD-FR-011, PRD-FR-012, PRD-FR-013, PRD-FR-014, PRD-FR-015, PRD-FR-016, PRD-FR-017, PRD-FR-018, PRD-FR-019, PRD-FR-020, PRD-FR-021, PRD-FR-022, PRD-FR-023, PRD-FR-024, PRD-FR-025, PRD-FR-026, PRD-FR-027, PRD-FR-028]
---

# System requirements specification

The product requirements are normative and are repeated in the machine-readable catalog. This document adds system-wide invariants and state semantics.

## Functional requirements

| ID | Priority | Requirement | Acceptance |
| --- | --- | --- | --- |
| PRD-FR-001 | Must | Recursively discover supported files from files, directories, response files, and standard input while applying deterministic include/exclude rules. | AC-001;AC-002 |
| PRD-FR-002 | Must | Provide `upright doctor` and `upright inspect` to report codecs, model/runtime availability, metadata, orientation transforms, filesystem constraints, and likely actions without writing files. | AC-003;AC-004 |
| PRD-FR-003 | Must | Read EXIF Orientation values 1–8, including mirrored cases, and distinguish stored-pixel geometry from intended display geometry. | AC-005 |
| PRD-FR-004 | Must | Evaluate 0°, 90°, 180°, and 270° additional rotations after applying the metadata display transform to an analysis representation. | AC-006;AC-007 |
| PRD-FR-005 | Must | Use local face detection and five-point landmarks to derive orientation evidence without performing face recognition. | AC-008;AC-009 |
| PRD-FR-006 | Must | Convert evidence into calibrated decision probabilities and choose REVIEW when score, margin, evidence, or safety rules are not satisfied. | AC-010;AC-011 |
| PRD-FR-007 | Must | The scan phase shall create a durable immutable plan containing file identity, fingerprints, evidence, selected action, policy version, model identity, and expected output. | AC-012;AC-013 |
| PRD-FR-008 | Must | Analysis is read-only; apply consumes a plan and refuses stale, changed, missing, or policy-incompatible inputs. | AC-014;AC-015 |
| PRD-FR-009 | Must | Default operation shall write corrected files to a separate output root while preserving relative paths and resolving collisions deterministically. | AC-016;AC-017 |
| PRD-FR-010 | Must | In-place changes require explicit acknowledgement, verified backups, write-ahead journal entries, same-volume staging, independent verification, and atomic replacement. | AC-018;AC-019;AC-020 |
| PRD-FR-011 | Must | For 90-degree JPEG rotations, prefer DCT-domain lossless transforms and apply an explicit policy to non-transformable MCU edge cases. | AC-021;AC-022 |
| PRD-FR-012 | Must | After successful physical normalization, set orientation metadata to 1 and preserve governed metadata according to the active policy. | AC-023;AC-024 |
| PRD-FR-013 | Must | Reopen every output through an independent read path and verify decodability, dimensions, orientation state, expected digest class, and required metadata before commit. | AC-025;AC-026 |
| PRD-FR-014 | Must | Persist checkpoints so scan, apply, verify, and undo can resume idempotently after cancellation, process termination, or reboot. | AC-027;AC-028 |
| PRD-FR-015 | Must | Restore verified backups only when current state matches the journal's expected post-apply state, otherwise abstain and report a conflict. | AC-029;AC-030 |
| PRD-FR-016 | Must | Provide list, show, decide, export, and import operations for REVIEW items, including thumbnails and evidence without requiring a graphical shell. | AC-031;AC-032 |
| PRD-FR-017 | Must | Support human-readable output plus schema-versioned JSON and JSON Lines, keeping machine stdout pure and progress/diagnostics on stderr. | AC-033;AC-034 |
| PRD-FR-018 | Must | Return documented, composable exit codes for success, review required, partial completion, invalid arguments/configuration, capability failure, stale plan, verification failure, and internal error. | AC-035 |
| PRD-FR-019 | Must | Load configuration with deterministic precedence and validate it against a versioned JSON Schema before work begins. | AC-036;AC-037 |
| PRD-FR-020 | Must | List, install, verify, activate, quarantine, and remove signed or digest-pinned model bundles using versioned manifests and rollback-safe activation. | AC-038;AC-039 |
| PRD-FR-021 | Should | Support JPEG and PNG at baseline and expose HEIC, AVIF, TIFF, and WebP only when decode, encode, metadata, and transform capabilities are verified. | AC-040 |
| PRD-FR-022 | Must | Run discovery, decode, inference, transform, and verification through bounded stages with backpressure, cancellation, and configurable resource limits. | AC-041;AC-042 |
| PRD-FR-023 | Must | Produce per-file events, aggregate summaries, provenance, timings, warnings, and reproducibility metadata without logging image content by default. | AC-043;AC-044 |
| PRD-FR-024 | Could | Optionally use bounded temporal or burst-group consensus as a tie-breaker, never as the sole basis for a high-confidence decision. | AC-045 |
| PRD-FR-025 | Should | Allow a compact local scene-orientation model as a gated fallback for no-face images only after evaluation proves incremental value without violating precision. | AC-046 |
| PRD-FR-026 | Should | Provide a benchmark command for representative decode, inference, transform, metadata, and end-to-end workloads, emitting environment and version metadata. | AC-047 |
| PRD-FR-027 | Should | Generate PowerShell completion and accept UTF-8 response files for robust automation with long input lists. | AC-048 |
| PRD-FR-028 | Must | List, inspect, validate, prune, and integrity-check jobs without applying image changes. | AC-049;AC-050 |

## System invariants

- **INV-001:** Scan cannot mutate source image content, metadata, timestamps, streams, or destination image files.
- **INV-002:** Apply cannot execute an action absent from the sealed plan, except an append-only validated review override.
- **INV-003:** A source fingerprint/identity mismatch cannot be downgraded to a warning.
- **INV-004:** A file reaches `Committed` only after output verification and durable journal state.
- **INV-005:** No failure path deletes the last verified source/backup copy.
- **INV-006:** Every terminal file state has a reason code and event.
- **INV-007:** Machine stdout is parseable according to the selected output mode.
- **INV-008:** The active model, runtime/provider, calibration, and policy versions are recorded with every ML decision.
- **INV-009:** Unsupported operation combinations fail in planning or preflight, before destructive work.
- **INV-010:** REVIEW is not automatically converted to a rotation by apply.

## File lifecycle

```text
Discovered
  -> Inspected
  -> Planned(Keep|Normalize|Rotate|Review|Unsupported|Error)
  -> Preflighted
  -> Staged
  -> Verified
  -> Committed
  -> [Undone]

Any transition may enter Failed or Conflict, but recovery must reconcile durable intent before retry.
```

## Conformance

A Must requirement is complete only when its acceptance criteria, mapped automated tests, documentation, and release evidence pass. Implementation existence alone is insufficient.
