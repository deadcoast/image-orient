---
document_id: DOC-NFR
title: Non-Functional Requirements
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-CHARTER, DOC-RESEARCH]
traces_to: [NFR-REL-001, NFR-REL-002, NFR-REL-003, NFR-REL-004, NFR-ML-001, NFR-ML-002, NFR-ML-003, NFR-ML-004, NFR-PERF-001, NFR-PERF-002, NFR-PERF-003, NFR-PERF-004, NFR-SEC-001, NFR-SEC-002, NFR-SEC-003, NFR-PRIV-001, NFR-PRIV-002, NFR-PRIV-003, NFR-UX-001, NFR-UX-002, NFR-COMP-001, NFR-COMP-002, NFR-FID-001, NFR-FID-002, NFR-MAINT-001, NFR-MAINT-002, NFR-OBS-001, NFR-OBS-002]
---

# Non-functional requirements

Targets are proposed release gates and must be confirmed or revised through measured spike/beta evidence.

| ID | Category | Normative statement | Target | Goals |
| --- | --- | --- | --- | --- |
| NFR-REL-001 | Reliability | No source file may be deleted, truncated, or replaced before a verified output and durable journal state exist. | 100% in fault-injection suite | GOAL-002 |
| NFR-REL-002 | Reliability | All mutating operations are idempotent or explicitly conflict-detecting under replay. | All replay scenarios pass | GOAL-002;GOAL-005 |
| NFR-REL-003 | Reliability | The application shall detect stale plans using file identity and content fingerprints before apply. | Zero undetected mutations in test matrix | GOAL-002;GOAL-004 |
| NFR-REL-004 | Reliability | A crash at any injected persistence boundary shall leave a recoverable, diagnosable state. | 100% crash-point recovery cases pass | GOAL-002;GOAL-006 |
| NFR-ML-001 | ML quality | Precision is the primary release metric; coverage may not be increased by weakening the false-rotation ceiling. | Accepted selfie precision ≥99.5% on governed corpus | GOAL-001;GOAL-008 |
| NFR-ML-002 | ML quality | Already-upright photos accepted for automation shall rarely be rotated incorrectly. | False rotation ≤0.1% on governed upright subset | GOAL-008 |
| NFR-ML-003 | ML quality | Target accepted-decision coverage only after precision targets are met. | Should target ≥85% selfie coverage | GOAL-001;GOAL-008 |
| NFR-ML-004 | ML quality | Calibration shall be measured by orientation-, device-, demographic-, lighting-, and occlusion-aware slices. | No critical slice violates release ceiling without documented block/mitigation | GOAL-001;GOAL-008 |
| NFR-PERF-001 | Performance | Memory usage shall be bounded independently of collection size. | Configurable ceiling; default working set target ≤1.5 GB on reference host | GOAL-005 |
| NFR-PERF-002 | Performance | Analysis shall avoid full-resolution decode when a bounded thumbnail is sufficient. | At least 95% of analyzed files use bounded decode path | GOAL-005 |
| NFR-PERF-003 | Performance | The pipeline shall keep CPU, GPU, storage, and metadata subprocess work bounded and observable. | No unbounded queue; stage metrics emitted | GOAL-005;GOAL-004 |
| NFR-PERF-004 | Performance | Cold-start and single-file inspection shall remain practical on supported hardware. | Doctor <3 s and inspect p95 <2 s on reference host, excluding first model install | GOAL-006 |
| NFR-SEC-001 | Security | Treat image files, metadata, model bundles, configuration, and job imports as untrusted input. | Threat-model controls and negative tests complete | GOAL-002;GOAL-007 |
| NFR-SEC-002 | Security | Third-party binaries and models shall be version-pinned, digest-verified, inventoried, and license-reviewed. | 100% production artifacts in SBOM and dependency register | GOAL-007 |
| NFR-SEC-003 | Security | No arbitrary in-process third-party plugin loading in v1. | Plugin boundary policy enforced by architecture tests | GOAL-002;GOAL-007 |
| NFR-PRIV-001 | Privacy | Core operation shall require no network access and shall not upload image pixels or metadata. | Network-denied end-to-end tests pass | GOAL-003 |
| NFR-PRIV-002 | Privacy | No face recognition, identity embeddings, or cross-file identity profiles may be created. | Design and binary review confirm absence | GOAL-003 |
| NFR-PRIV-003 | Privacy | Telemetry is disabled by default; diagnostic bundles redact paths unless explicitly requested. | Privacy review and golden diagnostics pass | GOAL-003;GOAL-004 |
| NFR-UX-001 | CLI UX | Destructive behavior shall require explicit, non-ambiguous flags and shall never be triggered by defaults. | CLI golden tests cover all destructive paths | GOAL-002;GOAL-006 |
| NFR-UX-002 | CLI UX | Human output shall be concise and actionable; machine output shall be stable, complete, and free of decoration. | Usability review plus schema/stream tests pass | GOAL-006 |
| NFR-COMP-001 | Compatibility | Primary release target is supported Windows 11 x64 with NTFS; behavior on ReFS, network shares, cloud placeholders, and removable media is capability-gated. | Published compatibility matrix backed by tests | GOAL-006 |
| NFR-COMP-002 | Compatibility | Paths shall support Unicode and Windows long-path semantics without lossy normalization. | Path conformance matrix passes | GOAL-005;GOAL-006 |
| NFR-FID-001 | Fidelity | Governed metadata preservation shall be field-aware and validated, not implemented as an unqualified byte-copy promise. | Metadata corpus round-trip policy passes | GOAL-002;GOAL-004 |
| NFR-FID-002 | Fidelity | JPEGs shall not be recompressed when a safe DCT-domain rotation is possible. | Coefficient or lossless-equivalence checks pass | GOAL-002 |
| NFR-MAINT-001 | Maintainability | Domain policy shall not depend directly on CLI, storage engine, model runtime, or native codec implementation. | Architecture conformance tests pass | GOAL-007 |
| NFR-MAINT-002 | Maintainability | All externally consumed schemas and exit codes shall be versioned with compatibility policy. | Contract compatibility suite passes | GOAL-004;GOAL-007 |
| NFR-OBS-001 | Supportability | Every job shall expose correlation IDs, stage timings, warnings, failure categories, runtime/model versions, and reproducibility data. | Support bundle completeness test passes | GOAL-004;GOAL-006 |
| NFR-OBS-002 | Supportability | Logs shall remain bounded through rotation/retention and shall not include image bytes. | Soak and privacy tests pass | GOAL-003;GOAL-005 |

## Priority rule

Reliability, privacy, security, and false-rotation constraints outrank throughput, package size, and accepted coverage. A performance improvement that changes decisions or fidelity requires full quality regression and an ADR/compatibility review.
