---
document_id: DOC-SPIKES
title: Risk-First Technical Spikes
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Program
depends_on: [DOC-RESEARCH, DOC-RISKS]
traces_to: [SPIKE-001, SPIKE-002, SPIKE-003, SPIKE-004, SPIKE-005, SPIKE-006, SPIKE-007, SPIKE-008, SPIKE-009, SPIKE-010]
---

# Technical spikes

## Rules

A spike is a bounded decision experiment, not production implementation. It must begin with a falsifiable question and exit gate. “It runs on my machine” is not sufficient. A failed hypothesis is a successful spike when it produces decisive evidence and an updated architecture.

## Priority/dependency view

| Spike | Primary question | Downstream blocks |
|---|---|---|
| SPIKE-001 | Corpus inventory and truth protocol | WP-007;WP-008;WP-020 |
| SPIKE-002 | Four-rotation YuNet orientation proof | WP-007;WP-008 |
| SPIKE-003 | Inference runtime and provider qualification | WP-006;WP-022 |
| SPIKE-004 | Metadata round-trip and ExifTool integration | WP-005;WP-010;WP-014 |
| SPIKE-005 | JPEG lossless rotation edge policy | WP-010;WP-014 |
| SPIKE-006 | Crash-safe atomic replace on Windows | WP-004;WP-011;WP-012;WP-015 |
| SPIKE-007 | Format and codec capability matrix | WP-005;WP-010;WP-022 |
| SPIKE-008 | Job-store durability and scale | WP-004;WP-009;WP-011;WP-017 |
| SPIKE-009 | CLI contract and automation ergonomics | WP-002;WP-018 |
| SPIKE-010 | Packaging, signing, and native dependency proof | WP-022;WP-023;WP-024 |

## SPIKE-001 — Corpus inventory and truth protocol

**Risk addressed:** Unknown real-world distribution and ambiguous ground truth.

**Work:** Create opt-in inventory scanner; define labeling guide, adjudication, privacy handling, dataset cards, slice taxonomy, and baseline corpus.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** At least 2,000 representative images or an approved smaller pilot; double-label agreement measured; no production photos copied without explicit consent.

**Blocks:** WP-007;WP-008;WP-020

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-002 — Four-rotation YuNet orientation proof

**Risk addressed:** Face landmarks may not reliably distinguish all rotations and poses.

**Work:** Run official YuNet ONNX across 0/90/180/270; implement geometric features; compare dynamic versus fixed input; measure latency, precision, coverage, and failure slices.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** A reproducible evaluation report; candidate approach can reach ≥99% precision on pilot accepted cases or documents why a secondary model is required.

**Blocks:** WP-007;WP-008

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-003 — Inference runtime and provider qualification

**Risk addressed:** Windows ML/ONNX Runtime packaging, provider selection, determinism, and cold-start behavior may differ by hardware.

**Work:** Prototype `IInferenceRuntime` against Windows ML self-contained and direct ONNX Runtime CPU; benchmark CPU/DirectML/available EPs; test provider fallback and offline installation.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** ADR update selects baseline runtime/provider policy with package-size, support, deterministic-result, and performance evidence.

**Blocks:** WP-006;WP-022

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-004 — Metadata round-trip and ExifTool integration

**Risk addressed:** Metadata families, maker notes, timestamps, XMP/IPTC, sidecars, and orientation writes can be damaged or reordered.

**Work:** Exercise ExifTool `-stay_open` adapter against device/format corpus; define preservation allow/deny/verify policy; test alternate data streams and timestamp restoration.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** Round-trip matrix and explicit guarantees/non-guarantees; no unexplained critical-field loss in baseline formats.

**Blocks:** WP-005;WP-010;WP-014

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-005 — JPEG lossless rotation edge policy

**Risk addressed:** MCU edge dimensions produce tradeoffs among refusal, trim, partial handling, or re-encode.

**Work:** Qualify libjpeg-turbo/jpegtran modes; build adversarial dimensions; compare pixels/coefficients, metadata markers, dimensions, and viewer compatibility.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** Default policy and CLI options approved; no silent crop/re-encode; test vectors committed.

**Blocks:** WP-010;WP-014

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-006 — Crash-safe atomic replace on Windows

**Risk addressed:** Antivirus, locks, cloud placeholders, network shares, power loss, and filesystem semantics can violate assumptions.

**Work:** Prototype same-directory staging, FlushFileBuffers, ReplaceFileW/MoveFileEx, backup semantics, handle-based identity, journal checkpoints, and fault injection.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** Recovery state machine proven on NTFS; unsupported filesystems/conditions capability-gated; fault matrix has no source-loss case.

**Blocks:** WP-004;WP-011;WP-012;WP-015

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-007 — Format and codec capability matrix

**Risk addressed:** HEIC/AVIF support varies with Windows extensions, WIC codecs, and native package licensing/distribution.

**Work:** Compare WIC and Magick.NET decode/encode/metadata paths; inventory licensing and redistributables; define operation-level capability reporting.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** Baseline format matrix approved; JPEG/PNG guaranteed; optional formats explicitly gated with tested diagnostics.

**Blocks:** WP-005;WP-010;WP-022

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-008 — Job-store durability and scale

**Risk addressed:** Long-running collections need resilient state transitions, migration, concurrency, and restart performance.

**Work:** Prototype SQLite schema, WAL/checkpoint policy, append-only events, plan sealing, batching, migrations, and 100k/1m synthetic job tests.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** Integrity, restart, migration, and performance targets pass with documented recovery procedure.

**Blocks:** WP-004;WP-009;WP-011;WP-017

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-009 — CLI contract and automation ergonomics

**Risk addressed:** Mixed human/machine output, cancellation, response files, and destructive confirmation can become unstable or unsafe.

**Work:** Prototype command tree, JSON/JSONL streams, exit codes, PowerShell completion, response files, progress redirection, and golden tests.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** CLI UX contract approved and versioned; all destructive cases require explicit flags; machine streams are parse-clean.

**Blocks:** WP-002;WP-018

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## SPIKE-010 — Packaging, signing, and native dependency proof

**Risk addressed:** Native binaries, models, code signing, installer behavior, AV reputation, and updates can derail release late.

**Work:** Build self-contained portable package and installer candidate; inventory SBOM/licenses; sign in test pipeline; run clean-VM and offline install/uninstall tests.

**Required artifacts**

- reproducible harness/source and fixture manifest;
- machine-readable raw results and summarized report;
- environment, version, digest, and configuration manifest;
- limitations and failed cases;
- recommended decision with alternatives;
- affected ADR, requirements, risks, and downstream work updates.

**Exit gate:** Reproducible signed candidate installs and runs on clean supported VMs; dependency notices and update/rollback strategy approved.

**Blocks:** WP-022;WP-023;WP-024

**Reviewers:** domain owner plus Architecture; Security/Release/Product added where relevant.


## Spike completion checklist

- [ ] Question and pass/fail threshold approved before experimentation.
- [ ] Inputs/fixtures and licenses/consent documented.
- [ ] Harness is repeatable from a clean checkout/environment.
- [ ] Raw output is machine-readable.
- [ ] Failure cases and resource/security behavior included.
- [ ] Recommendation compares at least one credible alternative where material.
- [ ] ADR and dependency register updated.
- [ ] Follow-on work package acceptance/test plan updated.
- [ ] Artifact retained under the evidence policy.
