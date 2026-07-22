---
document_id: DOC-DOD
title: Definition of Done and Release Gate Checklist
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Program
depends_on: [DOC-DEVPLAN, DOC-TEST, DOC-RELEASE]
traces_to: [M0, M1, M2, M3, M4, M5, M6, M7]
---

# Definition of Done

## Story/implementation item

- [ ] Requirement, acceptance, WP, component, and test IDs linked.
- [ ] Behavior and non-goals are clear.
- [ ] Domain/application/infrastructure boundaries respected.
- [ ] Expected errors are typed and reason-coded.
- [ ] Cancellation, retry, and resource limits considered.
- [ ] Unit/contract/integration tests pass.
- [ ] Security/privacy and untrusted-input behavior covered.
- [ ] Human and machine outputs documented/tested if affected.
- [ ] No new dependency/model/native asset without intake.
- [ ] Documentation/ADR/schema updated.
- [ ] CI green and reviewer approvals complete.

## Component/adaptor

- [ ] Conformance tests against shared contract.
- [ ] Capability/version probe.
- [ ] Success, unsupported, malformed, timeout, cancellation, and crash behavior.
- [ ] Thread/concurrency ownership documented.
- [ ] Provenance and diagnostics captured.
- [ ] Performance/resource baseline.
- [ ] License/SBOM/security record.
- [ ] Replacement fake/alternate demonstrated.

## Work package

- [ ] All upstream gates passed.
- [ ] Every mapped Must requirement acceptance test passes.
- [ ] Vertical workflow demonstrable from clean checkout/package.
- [ ] Evidence artifacts machine-readable and retained.
- [ ] Risks/assumptions/ADRs reconciled.
- [ ] Operations/support documentation updated.
- [ ] No unresolved critical defect.
- [ ] Downstream context packet generated.

## Milestone

- [ ] Entry/exit criteria verified.
- [ ] Integrated slice works safely on supported environment.
- [ ] Fault/recovery behavior appropriate to milestone passes.
- [ ] Compatibility matrix and known limitations current.
- [ ] Quality/performance/security evidence reviewed.
- [ ] Documentation validator and traceability pass.
- [ ] Product/Architecture/domain gate approvals recorded.

## Release candidate

- [ ] All Must functional and NFR obligations mapped to passing evidence.
- [ ] Accepted ML precision and false-rotation gates pass with confidence intervals.
- [ ] Zero source-loss fault cases.
- [ ] Metadata/JPEG baseline fidelity matrix passes.
- [ ] Network-denied/privacy tests pass.
- [ ] Critical/high security findings resolved or formally accepted.
- [ ] 100k bounded-resource soak passes.
- [ ] Supported compatibility matrix passes.
- [ ] Signed portable/installer artifacts pass clean-VM lifecycle.
- [ ] SBOM, notices, checksums, provenance, schemas, and release notes published.
- [ ] Backup/recovery/rollback drill completed.
- [ ] No unresolved journal/release evidence integrity issue.
