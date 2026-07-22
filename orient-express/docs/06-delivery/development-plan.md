---
document_id: DOC-DEVPLAN
title: Development Plan and Execution Governance
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Program
depends_on: [DOC-SPIKES, DOC-WBS, DOC-RISKS, ADR-0012]
traces_to: [M0, M1, M2, M3, M4, M5, M6, M7, WP-001, WP-002, WP-003, WP-004, WP-005, WP-006, WP-007, WP-008, WP-009, WP-010, WP-011, WP-012, WP-013, WP-014, WP-015, WP-016, WP-017, WP-018, WP-019, WP-020, WP-021, WP-022, WP-023, WP-024]
---

# Development plan

## Delivery strategy

PhotoUpright uses **risk-first, evidence-gated incremental delivery**. The team does not build the full CLI around unproven ML, metadata, JPEG, and filesystem assumptions. Each milestone leaves a usable vertical slice and a reproducible evidence pack.

## Critical path

```text
SPIKE-001 corpus/truth
  -> SPIKE-002 orientation proof
  -> WP-007 feature engine
  -> WP-008 calibration/policy
  -> WP-009 immutable planner
  -> WP-010 transform
  -> WP-013 independent verification
  -> WP-014 safe-copy E2E
  -> WP-015 guarded in-place
  -> WP-020 release calibration
  -> WP-024 release candidate
```

Filesystem, metadata, JPEG, runtime, codec, job-store, CLI, and packaging spikes run alongside the critical path but gate their corresponding work packages.

## Milestones

| ID | Milestone | Entry | Exit gate | Deliverables |
| --- | --- | --- | --- | --- |
| M0 | Evidence foundation | Approved charter and dev-spec baseline | SPIKE-001 through SPIKE-010 have owners, harnesses, and pass/fail gates; WP-001 complete | Research baseline; corpus protocol; spike reports; repository/CI baseline |
| M1 | Operational foundations | Blocking spikes for selected work packages pass | CLI, config, filesystem identity, metadata/codecs, model/runtime contracts work in diagnostic mode | doctor/inspect alpha; capability matrix; runtime/model/metadata prototypes |
| M2 | Read-only orientation planner | M1 complete and corpus available | Scan-only workflow produces immutable, explainable plans and meets pilot precision gate | orientation engine; calibration; SQLite jobs; scan command |
| M3 | Safe-copy processing | Planner stable and transform/recovery spikes passed | End-to-end safe-copy scan/apply/verify resumes safely and passes JPEG/metadata gates | transform; verify; bounded executor; safe-copy beta |
| M4 | Professional operations | Safe-copy beta stable | Review, in-place backup/undo, job integrity, reporting, and support diagnostics pass fault tests | review workflow; guarded in-place; undo; operational CLI |
| M5 | Quality and hardening | Feature-complete beta | ML, security, privacy, performance, soak, compatibility, and recovery release thresholds pass | evaluation report; threat review; benchmark report; compatibility matrix |
| M6 | Distribution candidate | M5 gates passed | Signed portable and installer candidates pass clean-VM/offline install, update, rollback, and SBOM checks | signed artifacts; installer; SBOM; checksums; provenance |
| M7 | Release candidate | Distribution candidate approved | All Must requirements and release ACs traced to passing evidence; no unaccepted critical risk | RC; release notes; support runbook; traceability evidence pack |

## Workstreams

### A — Product and CLI

Own command grammar, workflows, machine contracts, review usability, and documentation. Cannot weaken safety to simplify UX.

### B — Core and persistence

Own file identity, job store, immutable plans, bounded pipeline, journal, recovery, commit, undo, and integrity.

### C — Media I/O

Own format probe/decode, EXIF semantics, metadata preservation, JPEG DCT transforms, raster transforms, and verifier media checks.

### D — ML and quality

Own corpus governance, model/runtime experiments, features, calibration, thresholds, model/policy cards, and release metrics.

### E — Security and release

Own threat model, dependency intake, fuzzing, signing, SBOM, packaging, clean-VM matrix, update/rollback, and notices.

A small team may combine roles, but reviews remain cross-domain.

## First execution tranche

The next repository changes should be:

1. Create solution/project boundaries and architecture tests (`WP-001`).
2. Install documentation validation and requirement/test graph checks in CI.
3. Implement a read-only fixture/inventory tool for `SPIKE-001`.
4. Build metadata adapter spike with exact protocol fixtures (`SPIKE-004`).
5. Build JPEG edge fixture generator and jpegtran harness (`SPIKE-005`).
6. Build Windows atomic-replace/fault-injection harness (`SPIKE-006`).
7. Prototype CLI command/output contract (`SPIKE-009`).
8. Create model manifest and runtime harness (`SPIKE-002/003`).
9. Create capability matrix harness (`SPIKE-007`).
10. Create SQLite state-machine/load harness (`SPIKE-008`).
11. Create portable packaging skeleton and dependency inventory (`SPIKE-010`).

No spike result is a throwaway screenshot. It must produce code/fixtures, machine-readable results, a written conclusion, and an ADR update.

## Branch and review policy

- Trunk-based development with short-lived branches.
- Required CI and two reviews for safety/contract/model changes.
- Changes reference requirement, ADR, work-package, and test IDs.
- Feature flags may hide incomplete adapters, but dead/incomplete safety paths cannot masquerade as supported.
- Generated evidence is attached to CI/release storage, not casually committed when large or private.

## Work-package context packet

Before a WP enters implementation, generate:

- objective and mapped requirements/acceptance;
- upstream spike conclusions and artifacts;
- relevant ADRs and component contracts;
- schemas and compatibility constraints;
- threat/risk items;
- test/evidence plan;
- dependencies and downstream consumers;
- explicit out-of-scope items;
- Definition of Done checklist.

## Gate review

A milestone review answers:

1. Did all entry dependencies pass?
2. Are normative requirements and ADRs current?
3. Are measured results reproducible?
4. Did any assumption fail or introduce a new risk?
5. Are compatibility and migration consequences recorded?
6. Can the vertical slice be operated safely?
7. Is downstream work genuinely unblocked?

A failed gate changes the plan; it is not converted into a “known issue” without risk acceptance.

## Decision checkpoints

- After spikes: confirm stack and architecture.
- After scan planner: confirm ML precision/coverage feasibility.
- After safe-copy beta: confirm media fidelity and operational UX.
- Before in-place: independent safety review.
- Before distribution: security/license/package review.
- Before RC: complete traceability and rollback drill.

## Change budget

During M0–M2, design is expected to change based on evidence. From M3 onward, public contracts require compatibility discipline. A model/policy remains versionable even after CLI/schema freeze.

## Project completion

“Feature complete” means all planned commands exist. “Release ready” additionally means required quality, fault, security, compatibility, packaging, documentation, and traceability evidence passes.
