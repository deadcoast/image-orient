---
document_id: DOC-CHANGELOG
title: Development Specification Changelog
version: 0.1.3
status: baseline
as_of: 2026-07-23
owner: Architecture
depends_on: []
traces_to: []
---

# Changelog

## 0.1.3 — 2026-07-23

SPIKE-006 closure under single-operator governance:

- Marked SPIKE-006 `complete`: its exit gate (recovery state machine proven on NTFS, unsupported conditions capability-gated, zero source-loss fault matrix) is met by the recorded evidence.
- ADR-0009 0.1.1 records the qualification evidence: `ReplaceFileW(REPLACEFILE_WRITE_THROUGH)` is the sole in-place primitive; `MoveFileEx` replace is disqualified for in-place use.
- DOC-FSSAFE 0.1.1 records measured primitive behavior and the normative caveat that file IDs are pre-commit-only identity (re-baseline after commit).
- DOC-WBS 0.1.1 adds spike-driven design notes for WP-004, WP-011, WP-012, and WP-015.
- STATUS 0.1.3 records the single-operator governance mode: documentation gates (validator, CI, tests, evidence rules) are the control surface; review sign-offs are the operator's own acceptance.
- Affected IDs: SPIKE-006, ADR-0009, DOC-FSSAFE, DOC-WBS, DOC-STATUS, DOC-CHANGELOG, WP-004, WP-011, WP-012, WP-015. No schema or migration impact; no dependency changes.

## 0.1.2 — 2026-07-23

SPIKE-006 NTFS pilot evidence (crash-safe atomic replace):

- Added the SPIKE-006 decision harness under `spikes/SPIKE-006-atomic-replace/` (`OrientExpress.Spike006` plus its test project, registered in the solution): the DOC-FSSAFE in-place protocol with a durable JSON-lines journal, kernel-level crash injection at every step boundary, the recovery state machine, and primitive qualification of `ReplaceFileW`/`MoveFileEx`.
- Encoded the recovery state machine test-first: 31 unit tests, including matrix rows, torn-journal tolerance, and totality over the observation space, run in CI with the solution.
- Recorded machine-readable evidence (24 scenarios): all twelve fault-injection cases recover per the DOC-FSSAFE matrix with zero source-loss cases and idempotent re-recovery; `ReplaceFileW` preserves creation time, attributes, alternate data streams, and DACL but **not** the NTFS file ID; `MoveFileEx` replace loses creation time, ADS, and identity; sharing/read-only/missing-target error classes measured; cross-volume rejection stays capability-gated (single-volume machine).
- Recorded the WP-004 design consequence: handle-based identity must re-baseline after commit because the file ID does not survive `ReplaceFileW`.
- Updated the spike register (SPIKE-006 → evidence-recorded-pending-review) and STATUS; in-place mode remains blocked pending review sign-off, non-NTFS capability classes, and undo-path evidence.
- Affected IDs: SPIKE-006, RISK-005, ADR-0009 (evidence pending review), WP-004, WP-011, WP-012, WP-015, DOC-FSSAFE (reviewed follow-up), DOC-STATUS, DOC-CHANGELOG. No schema or migration impact; no new package dependencies (NFR-SEC-002 unchanged).

## 0.1.1 — 2026-07-22

WP-001 repository and engineering baseline:

- Implemented the `OrientExpress` solution layout: nine source projects plus unit and architecture test projects per DOC-ARCH. Project names use the namespace-safe `OrientExpress.*` form (C# namespaces cannot contain hyphens); the CLI builds as the provisional executable `upright`.
- Enforced NFR-MAINT-001 layer rules with architecture conformance tests (harness for TEST-NFR-025).
- Established deterministic builds toward NFR-SEC-002: `global.json` SDK pin, central package management, NuGet lock files with locked-mode CI restore, single mapped package source, warnings-as-errors, analyzers, and an `.editorconfig` style gate.
- Added GitHub Actions CI: documentation/catalog/traceability validation, dependency-graph regeneration check, and Windows build, format, and test jobs.
- Adopted xunit.v3 3.2.2 per DEP-012 and inventoried test tooling as DEP-015 through DEP-017 with sources SRC-029 and SRC-030.
- Repaired a broken export link in `research/spec-02.md` left over from the PhotoUpright rename; added `.gitignore`/`.gitattributes` and stopped tracking Python bytecode.
- Removed the stale one-shot package artifacts `SHA256SUMS.txt` and `VALIDATION-REPORT.txt`: validation now runs in CI on every change, and release checksums are produced at packaging time per DOC-CICD.
- Affected IDs: WP-001, NFR-MAINT-001, NFR-SEC-002, DEP-012, DOC-ARCH, DOC-STATUS, DOC-CHANGELOG. No schema or migration impact.

## 0.1.0 — 2026-07-21

Initial professional planning baseline:

- Established product charter, scope, workflows, 28 functional requirements, 28 non-functional requirements, and 50 acceptance criteria.
- Defined Windows-first modular-monolith architecture and 24 components.
- Defined EXIF-plus-inferred-transform semantics and a calibrated four-rotation orientation pipeline.
- Established immutable scan/apply jobs, safe-copy defaults, guarded in-place recovery, JPEG fidelity policy, metadata policy, and output verification.
- Created ten technical spikes, 24 work packages, eight evidence-gated milestones, 20 risks, and 12 ADRs.
- Added five external JSON Schemas, machine-readable catalogs, dependency graphs, and validation tooling.

Future entries must identify affected requirement, ADR, schema, and migration IDs.
