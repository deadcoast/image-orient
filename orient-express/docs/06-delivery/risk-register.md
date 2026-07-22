---
document_id: DOC-RISKS
title: Risk Register
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Program
depends_on: [DOC-RESEARCH, DOC-CHARTER]
traces_to: [RISK-001, RISK-002, RISK-003, RISK-004, RISK-005, RISK-006, RISK-007, RISK-008, RISK-009, RISK-010, RISK-011, RISK-012, RISK-013, RISK-014, RISK-015, RISK-016, RISK-017, RISK-018, RISK-019, RISK-020]
---

# Risk register

Probability/impact are planning assessments, not measured incident rates. Critical risks cannot be accepted informally.

| ID | Category | Risk | Probability | Impact | Mitigation | Trigger | Response | Owner |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RISK-001 | ML | Landmark geometry is unreliable for extreme pose, partial faces, occlusion, or artistic orientation. | High | High | Calibrated abstention; slice evaluation; optional scene fallback only after proof. | Accepted precision below gate in any critical slice | Block release or reduce coverage. | ML |
| RISK-002 | ML | Corpus overrepresents easy selfies and hides device/demographic/lighting failure modes. | High | High | Dataset cards, slice quotas, double labeling, holdout and external pilot. | Large metric spread or label disagreement | Expand/rebalance corpus and recalibrate. | Quality |
| RISK-003 | Metadata | Maker notes or metadata offsets are damaged during transform/write. | Medium | Critical | ExifTool qualification, field-aware policy, before/after diff, safe-copy default. | Unexplained critical metadata delta | Block format/device combination. | Media I/O |
| RISK-004 | JPEG | Non-MCU-aligned JPEG transform silently crops or recompresses edges. | Medium | High | Explicit policy, dimensions/test vectors, never-silent diagnostics. | Pixel/dimension mismatch | Refuse or use approved fallback. | Media I/O |
| RISK-005 | Filesystem | Crash, lock, antivirus, or power interruption causes source loss during in-place replacement. | Medium | Critical | Same-volume staging, flush, verified backup, journal, atomic replace, fault injection. | Any unrecoverable fault case | Disable in-place release. | Core |
| RISK-006 | Filesystem | Cloud placeholders, reparse points, hardlinks, ADS, or network shares violate local-file assumptions. | High | High | Capability probe, handle identity, safe defaults, explicit unsupported states. | Identity mismatch or non-atomic semantics | Safe-copy only or refuse. | Core |
| RISK-007 | Runtime | GPU/NPU execution provider produces inconsistent output or deployment failures across hardware. | Medium | High | CPU deterministic baseline, provider qualification, fallback and provenance. | Decision divergence beyond tolerance | Pin/disable provider for decisions. | ML Platform |
| RISK-008 | Packaging | Native/model dependencies increase package size, trigger antivirus, or complicate signing. | Medium | Medium | Early packaging spike, clean-VM testing, signed binaries, SBOM and provenance. | Install or AV block on supported VM | Repackage or replace dependency. | Release |
| RISK-009 | Licensing | Third-party binary/model license or redistribution terms do not fit distribution. | Medium | Critical | Dependency register and legal review before adoption; adapter allows substitution. | Unresolved redistribution obligation | Block dependency; choose alternative. | Release |
| RISK-010 | Security | Malformed image or metadata exploits native decoder/tool. | Medium | Critical | Patched dependencies, limits, timeouts, fuzzing, least privilege, subprocess isolation where feasible. | Crash/hang/memory violation | Quarantine input and patch/replace component. | Security |
| RISK-011 | Privacy | Logs, thumbnails, or support bundles expose private paths or images. | Medium | High | No thumbnails by default, redaction, retention policy, explicit export. | Privacy test finds content leakage | Block release and purge artifacts. | Security |
| RISK-012 | Scale | SQLite contention or unbounded queues degrade large jobs. | Medium | High | Single-writer batches, bounded channels, synthetic 100k/1m tests, checkpoints. | Memory growth or restart SLA miss | Tune schema/batching or partition jobs. | Core |
| RISK-013 | UX | Users misinterpret portrait/landscape as aspect ratio rather than upright orientation. | High | Medium | Use 'upright orientation' terminology; inspect explanations and previews. | Usability test confusion | Revise language and help. | Product |
| RISK-014 | UX | A one-command run encourages unsafe in-place expectations. | Medium | High | Safe-copy default, explicit in-place acknowledgement, dry-run summaries. | User attempts destructive default | Strengthen guardrails. | Product |
| RISK-015 | Compatibility | HEIC/AVIF availability differs by Windows installation and extensions. | High | Medium | Operation-level capability matrix and doctor diagnostics; baseline does not promise unsupported codecs. | Codec absent/incompatible | Explain installation or mark unsupported. | Media I/O |
| RISK-016 | Product | Intentional sideways/lying-down selfies are 'corrected' against artistic intent. | Medium | High | Abstention, sequence context as weak evidence, review queue, never infer aesthetic mirror. | Intentional-orientation false positives | Add uncertainty feature/slice and raise threshold. | ML |
| RISK-017 | Supply chain | Model or binary update changes behavior without traceability. | Medium | High | Digest pinning, signed manifests, compatibility tests, atomic activation/rollback. | Unapproved digest/version | Quarantine and retain prior active version. | Release |
| RISK-018 | Performance | Four rotations multiply inference cost and make HDD-heavy collections slow. | High | Medium | Bounded thumbnails, batching where proven, metadata fast path, caching, provider acceleration. | Throughput below release floor | Optimize without weakening accuracy/safety. | Performance |
| RISK-019 | Governance | Documentation and implementation drift, breaking traceability. | Medium | High | Machine-readable IDs, CI validator, ADRs, requirement/test gates. | Orphan or unknown ID in CI | Fail build until reconciled. | Architecture |
| RISK-020 | Support | Failure reports lack enough provenance to reproduce field issues. | Medium | Medium | Schema-versioned events, environment/model/runtime manifests, redacted support bundles. | Issue cannot be reproduced from job bundle | Extend event/support contract. | Operations |

## Review cadence

- At every spike conclusion and milestone gate.
- Immediately after a source-loss, false-rotation, metadata-loss, security, or compatibility discovery.
- Before enabling in-place mode.
- Before production dependency/model updates.
- At release candidate approval.

## Risk acceptance

A risk acceptance names scope, owner, evidence, user impact, expiry/review date, mitigation, monitoring, and rollback. Critical source-loss/privacy/security risks and release precision failures require executive/product plus domain approval and normally block release.
