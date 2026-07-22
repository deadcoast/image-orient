---
document_id: DOC-CHANGELOG
title: Development Specification Changelog
version: 0.1.1
status: baseline
as_of: 2026-07-22
owner: Architecture
depends_on: []
traces_to: []
---

# Changelog

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
