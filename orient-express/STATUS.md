---
document_id: DOC-STATUS
title: PhotoUpright Planning Status
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Program
depends_on: [DOC-DEVPLAN, DOC-SPIKES, DOC-WBS]
traces_to: []
---

# Planning status

## Current state

The project is at **design baseline / pre-implementation**. Product intent, requirements, architecture, risk model, technical spikes, work packages, milestones, draft schemas, and traceability structure are defined. No production source code or measured accuracy claim is represented as complete.

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
| In-place mode | SPIKE-006 plus fault-injection evidence |
| HEIC/AVIF claims | SPIKE-007 capability matrix |
| Final job-store settings | SPIKE-008 scale/durability report |
| Frozen CLI v1 contract | SPIKE-009 usability/automation evidence |
| Signed distribution shape | SPIKE-010 clean-VM proof |

## Next controlled execution sequence

1. Complete WP-001 and establish CI documentation/architecture checks.
2. Run SPIKE-001, SPIKE-004, SPIKE-005, SPIKE-006, and SPIKE-009 in parallel where ownership permits.
3. Use the first corpus tranche from SPIKE-001 to run SPIKE-002 and SPIKE-003.
4. Run codec/package spikes before selecting final native distribution.
5. Update provisional ADRs with evidence and only then unlock M1/M2 implementation packages.

See [technical spikes](docs/06-delivery/technical-spikes.md) and [work breakdown](docs/06-delivery/work-breakdown-structure.md).
