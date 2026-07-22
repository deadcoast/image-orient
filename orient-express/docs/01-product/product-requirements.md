---
document_id: DOC-PRD
title: Product Requirements
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Product
depends_on: [DOC-CHARTER, DOC-WORKFLOWS, DOC-SCOPE]
traces_to: [PRD-FR-001, PRD-FR-002, PRD-FR-003, PRD-FR-004, PRD-FR-005, PRD-FR-006, PRD-FR-007, PRD-FR-008, PRD-FR-009, PRD-FR-010, PRD-FR-011, PRD-FR-012, PRD-FR-013, PRD-FR-014, PRD-FR-015, PRD-FR-016, PRD-FR-017, PRD-FR-018, PRD-FR-019, PRD-FR-020, PRD-FR-021, PRD-FR-022, PRD-FR-023, PRD-FR-024, PRD-FR-025, PRD-FR-026, PRD-FR-027, PRD-FR-028]
---

# Product requirements

Requirements use MoSCoW priority. Must requirements are release blockers unless explicitly deferred through product governance.

| ID | Priority | Title | Normative statement | Goals | Components | Acceptance |
| --- | --- | --- | --- | --- | --- | --- |
| PRD-FR-001 | Must | Discover inputs | Recursively discover supported files from files, directories, response files, and standard input while applying deterministic include/exclude rules. | GOAL-001;GOAL-005;GOAL-006 | COMP-CLI;COMP-DISCOVERY | AC-001;AC-002 |
| PRD-FR-002 | Must | Inspect capabilities | Provide `upright doctor` and `upright inspect` to report codecs, model/runtime availability, metadata, orientation transforms, filesystem constraints, and likely actions without writing files. | GOAL-004;GOAL-006 | COMP-CLI;COMP-CAPABILITIES;COMP-METADATA | AC-003;AC-004 |
| PRD-FR-003 | Must | Read EXIF orientation | Read EXIF Orientation values 1–8, including mirrored cases, and distinguish stored-pixel geometry from intended display geometry. | GOAL-001;GOAL-002 | COMP-METADATA;COMP-TRANSFORM | AC-005 |
| PRD-FR-004 | Must | Analyze four upright candidates | Evaluate 0°, 90°, 180°, and 270° additional rotations after applying the metadata display transform to an analysis representation. | GOAL-001;GOAL-008 | COMP-ORIENTATION;COMP-INFERENCE | AC-006;AC-007 |
| PRD-FR-005 | Must | Detect and score faces | Use local face detection and five-point landmarks to derive orientation evidence without performing face recognition. | GOAL-001;GOAL-003;GOAL-008 | COMP-INFERENCE;COMP-FEATURES | AC-008;AC-009 |
| PRD-FR-006 | Must | Calibrate confidence and abstain | Convert evidence into calibrated decision probabilities and choose REVIEW when score, margin, evidence, or safety rules are not satisfied. | GOAL-004;GOAL-008 | COMP-POLICY;COMP-CALIBRATION | AC-010;AC-011 |
| PRD-FR-007 | Must | Create immutable plans | The scan phase shall create a durable immutable plan containing file identity, fingerprints, evidence, selected action, policy version, model identity, and expected output. | GOAL-002;GOAL-004;GOAL-006 | COMP-PLANNER;COMP-JOBSTORE | AC-012;AC-013 |
| PRD-FR-008 | Must | Separate analysis and apply | Analysis is read-only; apply consumes a plan and refuses stale, changed, missing, or policy-incompatible inputs. | GOAL-002;GOAL-004 | COMP-PLANNER;COMP-EXECUTOR;COMP-JOBSTORE | AC-014;AC-015 |
| PRD-FR-009 | Must | Safe-copy output | Default operation shall write corrected files to a separate output root while preserving relative paths and resolving collisions deterministically. | GOAL-002;GOAL-006 | COMP-EXECUTOR;COMP-FILESYSTEM | AC-016;AC-017 |
| PRD-FR-010 | Must | Guarded in-place output | In-place changes require explicit acknowledgement, verified backups, write-ahead journal entries, same-volume staging, independent verification, and atomic replacement. | GOAL-002 | COMP-EXECUTOR;COMP-FILESYSTEM;COMP-JOURNAL | AC-018;AC-019;AC-020 |
| PRD-FR-011 | Must | Lossless JPEG transform | For 90-degree JPEG rotations, prefer DCT-domain lossless transforms and apply an explicit policy to non-transformable MCU edge cases. | GOAL-002 | COMP-CODEC;COMP-TRANSFORM | AC-021;AC-022 |
| PRD-FR-012 | Must | Normalize orientation metadata | After successful physical normalization, set orientation metadata to 1 and preserve governed metadata according to the active policy. | GOAL-001;GOAL-002 | COMP-METADATA;COMP-TRANSFORM | AC-023;AC-024 |
| PRD-FR-013 | Must | Verify outputs independently | Reopen every output through an independent read path and verify decodability, dimensions, orientation state, expected digest class, and required metadata before commit. | GOAL-002;GOAL-004 | COMP-VERIFY;COMP-FILESYSTEM | AC-025;AC-026 |
| PRD-FR-014 | Must | Resume interrupted jobs | Persist checkpoints so scan, apply, verify, and undo can resume idempotently after cancellation, process termination, or reboot. | GOAL-002;GOAL-005;GOAL-006 | COMP-JOBSTORE;COMP-JOURNAL;COMP-EXECUTOR | AC-027;AC-028 |
| PRD-FR-015 | Must | Undo in-place changes | Restore verified backups only when current state matches the journal's expected post-apply state, otherwise abstain and report a conflict. | GOAL-002;GOAL-004 | COMP-JOURNAL;COMP-EXECUTOR | AC-029;AC-030 |
| PRD-FR-016 | Must | Review uncertain items | Provide list, show, decide, export, and import operations for REVIEW items, including thumbnails and evidence without requiring a graphical shell. | GOAL-006;GOAL-008 | COMP-REVIEW;COMP-CLI | AC-031;AC-032 |
| PRD-FR-017 | Must | Human and machine output | Support human-readable output plus schema-versioned JSON and JSON Lines, keeping machine stdout pure and progress/diagnostics on stderr. | GOAL-004;GOAL-006 | COMP-CLI;COMP-REPORTING | AC-033;AC-034 |
| PRD-FR-018 | Must | Stable exit codes | Return documented, composable exit codes for success, review required, partial completion, invalid arguments/configuration, capability failure, stale plan, verification failure, and internal error. | GOAL-006 | COMP-CLI | AC-035 |
| PRD-FR-019 | Must | Validate configuration | Load configuration with deterministic precedence and validate it against a versioned JSON Schema before work begins. | GOAL-004;GOAL-006 | COMP-CONFIG;COMP-CLI | AC-036;AC-037 |
| PRD-FR-020 | Must | Model lifecycle | List, install, verify, activate, quarantine, and remove signed or digest-pinned model bundles using versioned manifests and rollback-safe activation. | GOAL-003;GOAL-004;GOAL-007 | COMP-MODELS;COMP-INFERENCE | AC-038;AC-039 |
| PRD-FR-021 | Should | Capability-gated formats | Support JPEG and PNG at baseline and expose HEIC, AVIF, TIFF, and WebP only when decode, encode, metadata, and transform capabilities are verified. | GOAL-001;GOAL-006 | COMP-CAPABILITIES;COMP-CODEC | AC-040 |
| PRD-FR-022 | Must | Bounded concurrency | Run discovery, decode, inference, transform, and verification through bounded stages with backpressure, cancellation, and configurable resource limits. | GOAL-005;GOAL-006 | COMP-PIPELINE;COMP-EXECUTOR | AC-041;AC-042 |
| PRD-FR-023 | Must | Audit and reporting | Produce per-file events, aggregate summaries, provenance, timings, warnings, and reproducibility metadata without logging image content by default. | GOAL-003;GOAL-004;GOAL-006 | COMP-REPORTING;COMP-JOBSTORE | AC-043;AC-044 |
| PRD-FR-024 | Could | Sequence consensus | Optionally use bounded temporal or burst-group consensus as a tie-breaker, never as the sole basis for a high-confidence decision. | GOAL-001;GOAL-008 | COMP-POLICY;COMP-DISCOVERY | AC-045 |
| PRD-FR-025 | Should | Scene fallback | Allow a compact local scene-orientation model as a gated fallback for no-face images only after evaluation proves incremental value without violating precision. | GOAL-001;GOAL-008 | COMP-INFERENCE;COMP-POLICY | AC-046 |
| PRD-FR-026 | Should | Benchmark command | Provide a benchmark command for representative decode, inference, transform, metadata, and end-to-end workloads, emitting environment and version metadata. | GOAL-005;GOAL-007 | COMP-BENCHMARK;COMP-REPORTING | AC-047 |
| PRD-FR-027 | Should | Shell completion and response files | Generate PowerShell completion and accept UTF-8 response files for robust automation with long input lists. | GOAL-006 | COMP-CLI | AC-048 |
| PRD-FR-028 | Must | Job integrity tools | List, inspect, validate, prune, and integrity-check jobs without applying image changes. | GOAL-002;GOAL-004;GOAL-006 | COMP-JOBSTORE;COMP-CLI | AC-049;AC-050 |

## Product policy

- `scan` is always read-only.
- Safe-copy is the default output mode.
- `apply --in-place` requires an explicit acknowledgement token/flag and successful backup preflight.
- Automatic actions are accepted only when ML and safety policy gates pass.
- REVIEW is a successful, expected outcome, not an internal error.
- Unsupported capability is discovered during planning whenever practical.
- No command silently changes JPEG edge policy, metadata policy, active model, or output collision policy.
