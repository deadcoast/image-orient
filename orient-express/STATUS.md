---
document_id: DOC-STATUS
title: orient-express Planning Status
version: 0.1.3
status: baseline
as_of: 2026-07-23
owner: Program
depends_on: [DOC-DEVPLAN, DOC-SPIKES, DOC-WBS]
traces_to: []
---

# Planning status

## Current state

The project is at **engineering baseline / pre-implementation**. Product intent, requirements, architecture, risk model, technical spikes, work packages, milestones, draft schemas, and traceability structure are defined. WP-001 is implemented pending CI verification and review: the `OrientExpress` solution and project boundaries exist with architecture conformance tests (NFR-MAINT-001), analyzers/formatting/warnings-as-errors, deterministic pinned builds with locked packages, and a CI pipeline that gates documentation, build, style, and tests. No production feature code or measured accuracy claim is represented as complete.

SPIKE-006 is **complete** ([report](spikes/SPIKE-006-atomic-replace/report.md)): the DOC-FSSAFE recovery state machine is proven on NTFS by unit tests and a twelve-case crash fault matrix with zero source-loss cases, and the atomic primitives are measured (notably: `ReplaceFileW` preserves creation time, attributes, ADS, and DACL, but not the NTFS file ID — identity re-baselines after commit). Its exit gate is met with non-NTFS classes capability-gated; ADR-0009, DOC-FSSAFE, and DOC-WBS carry the consequences. In-place mode still ships only after the full fault suite across supported compatibility classes and the undo path (WP-015 gate). The project operates under single-operator governance: the documentation gates (validator, CI, tests, evidence rules) are the control surface; multi-role review sign-offs are recorded as the operator's own acceptance.

## Approved to begin

- Repository and CI baseline.
- Corpus governance and truth protocol.
- Read-only prototypes for metadata, decode, model runtime, and file identity.
- All ten risk-first technical spikes.
- CLI contract prototype and schema review.

## Blocked until evidence exists

| Capability | Blocking evidence |
|---|---|
| Production orientation thresholds | SPIKE-001 and SPIKE-002 reports |
| Selected inference provider | SPIKE-003 report and ADR update |
| Metadata guarantee language | SPIKE-004 round-trip matrix |
| Default JPEG edge policy | SPIKE-005 report |
| In-place mode | Full fault suite across supported compatibility classes plus undo-path evidence (SPIKE-006 NTFS pilot complete 2026-07-23) |
| HEIC/AVIF claims | SPIKE-007 capability matrix |
| Final job-store settings | SPIKE-008 scale/durability report |
| Frozen CLI v1 contract | SPIKE-009 usability/automation evidence |
| Signed distribution shape | SPIKE-010 clean-VM proof |

## Next controlled execution sequence

1. Keep WP-001 CI documentation/architecture checks green (baseline established 2026-07-22).
2. Run SPIKE-001, SPIKE-004, SPIKE-005, SPIKE-006, and SPIKE-009 in parallel where ownership permits.
3. Use the first corpus tranche from SPIKE-001 to run SPIKE-002 and SPIKE-003.
4. Run codec/package spikes before selecting final native distribution.
5. Update provisional ADRs with evidence and only then unlock M1/M2 implementation packages.

See [technical spikes](docs/06-delivery/technical-spikes.md) and [work breakdown](docs/06-delivery/work-breakdown-structure.md).
