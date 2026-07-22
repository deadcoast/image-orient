---
document_id: DOC-MASTER
title: PhotoUpright Master Development Specification
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-CHARTER, DOC-PRD, DOC-SRS, DOC-ARCH, DOC-ML, DOC-FSSAFE, DOC-DEVPLAN, DOC-TEST]
traces_to: [GOAL-001, GOAL-002, GOAL-003, GOAL-004, GOAL-005, GOAL-006, GOAL-007, GOAL-008]
---

# Master specification

## Mission

PhotoUpright will inspect large Windows photo collections, determine the intended **upright display orientation**, and safely normalize files or route ambiguous cases to review. The product is optimized for selfie-heavy libraries but must remain honest about no-face, multi-face, artistic, damaged, unsupported, and low-confidence images.

The target is not a simplistic width-versus-height classifier. A stored JPEG may be landscape-shaped while metadata requests a portrait display; EXIF can also encode mirrored transforms. The system therefore models:

```text
stored pixels
  + metadata display transform (EXIF Orientation 1–8)
  + inferred additional quarter-turn (0/90/180/270)
  = normalized upright output
```

## Governing principles

1. **Do no harm.** Safe-copy is the default, in-place is guarded, and verification precedes commit.
2. **Precision before coverage.** An uncertain image is REVIEW, not a forced guess.
3. **Offline by design.** Image data stays local; face detection is not face recognition.
4. **Plan before mutation.** Scan and apply are distinct, durable, auditable phases.
5. **No silent fidelity tradeoff.** JPEG trim/re-encode and metadata limitations are explicit.
6. **Evidence gates implementation.** High-risk technical assumptions must pass spikes before broad work proceeds.
7. **Automation is a contract.** JSON schemas, exit codes, reason codes, model/runtime versions, and config precedence are stable and tested.

## Product boundary

### Release-one Must capability

- Deterministic discovery and inspection.
- EXIF Orientation 1–8 handling.
- Four-rotation selfie orientation inference.
- Calibrated confidence and abstention.
- Immutable scan plans and stale-plan refusal.
- JPEG/PNG baseline support.
- Lossless eligible JPEG quarter-turns.
- Metadata normalization and field-aware preservation.
- Safe-copy output, independent verification, resumability, and reports.
- Guarded in-place mode with backups, journal, recovery, and undo.
- Human plus JSON/JSONL interfaces.
- Model lifecycle, job integrity, diagnostics, and offline packaging.

### Explicit non-goals for v1

- General photo organization, face recognition, duplicate detection, tagging, editing, cloud sync, or a full desktop GUI.
- Inferring whether a user aesthetically prefers a mirrored selfie.
- Guaranteeing orientation for every image.
- Treating HEIC/AVIF as supported merely because a decoder happens to be installed.
- Arbitrary in-process third-party plugins.
- NativeAOT or cross-platform distribution without qualification.

## System shape

```text
CLI Host
  ├─ configuration / capability probe
  ├─ scan planner
  │    ├─ deterministic discovery and file identity
  │    ├─ metadata and bounded decode
  │    ├─ four-candidate ONNX inference
  │    ├─ feature extraction, calibration, policy
  │    └─ immutable SQLite job plan
  ├─ apply executor
  │    ├─ stale-input preflight
  │    ├─ JPEG DCT or raster transform
  │    ├─ metadata normalization
  │    ├─ independent verification
  │    └─ atomic commit / journal / backup
  ├─ review workflow
  └─ JSON/JSONL events, summaries, diagnostics
```

Detailed boundaries are in [system architecture](docs/04-architecture/system-architecture.md) and [component contracts](docs/04-architecture/component-contracts.md).

## Decision classes

| Decision | Meaning |
|---|---|
| `KEEP` | Pixels and metadata already produce the selected upright display. |
| `NORMALIZE_METADATA` | Display is upright but pixels/metadata should be physically normalized. |
| `ROTATE_90_CW` | Apply an additional clockwise quarter-turn after metadata semantics are resolved. |
| `ROTATE_180` | Apply an additional half-turn. |
| `ROTATE_270_CW` | Apply an additional three-quarter clockwise turn. |
| `REVIEW` | Evidence or safety policy is insufficient for automatic action. |
| `UNSUPPORTED` | Required operation is unavailable for the file/environment. |
| `ERROR` | Inspection or processing failed in a classified way. |

## Proposed release quality gates

These are engineering targets to prove, not claims about an unbuilt product.

| Gate | Proposed threshold |
|---|---|
| Accepted selfie decision precision | ≥ 99.5% on governed holdout |
| False rotation of already-upright accepted files | ≤ 0.1% |
| Accepted selfie coverage | Should reach ≥ 85% only after precision passes |
| Source-loss fault tests | 0 source-loss cases |
| Critical metadata unexplained loss | 0 in guaranteed baseline matrix |
| Machine-output schema validity | 100% |
| 100k-file bounded-memory soak | Pass configured resource ceiling |
| Network-denied core workflow | Pass |
| Critical/high security findings | 0 unaccepted |

## Delivery gates

- **M0:** prove corpus, ML, runtime, metadata, JPEG, filesystem, codecs, job store, CLI, and packaging assumptions.
- **M1:** diagnostic foundations.
- **M2:** read-only orientation planner.
- **M3:** verified safe-copy processing.
- **M4:** review, guarded in-place, undo, and operations.
- **M5:** quality, security, privacy, performance, compatibility.
- **M6:** signed distribution.
- **M7:** release candidate with complete trace evidence.

See [development plan](docs/06-delivery/development-plan.md).

## Governance rule

A code change that affects a public behavior, safety guarantee, schema, model decision, dependency, or release gate must update its requirement/ADR/test mappings in the same change. CI runs the documentation validator and treats traceability breakage as a build failure.
