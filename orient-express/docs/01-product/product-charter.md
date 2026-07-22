---
document_id: DOC-CHARTER
title: Product Charter
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Product
depends_on: [DOC-RESEARCH]
traces_to: [GOAL-001, GOAL-002, GOAL-003, GOAL-004, GOAL-005, GOAL-006, GOAL-007, GOAL-008]
---

# Product charter

## Problem

Large photo libraries frequently contain files whose stored pixels or orientation metadata cause sideways or upside-down display. Manual correction is slow, repetitive, and risky. Existing autorotation tools usually trust EXIF metadata; they do not reliably infer orientation when metadata is missing, normalized incorrectly, or stripped. A selfie-heavy collection provides a useful signal—face geometry—but mistakes are highly visible and overwriting originals is unacceptable.

## Product promise

PhotoUpright will make orientation cleanup safe enough to automate and transparent enough to trust:

- inspect before changing;
- infer upright orientation locally;
- explain and calibrate each decision;
- abstain when uncertain;
- preserve image and metadata fidelity;
- verify outputs and recover from interruption;
- integrate cleanly into Windows automation.

## Goals

| ID | Goal | Statement | Measure |
| --- | --- | --- | --- |
| GOAL-001 | Correct orientation automatically | Correct display orientation for large photo collections with particular strength on selfie-heavy libraries. | Accepted decisions meet the release precision target on the governed evaluation corpus. |
| GOAL-002 | Protect originals and metadata | Prevent silent data loss, corruption, avoidable recompression, or metadata damage. | Destructive fault-injection and metadata round-trip gates pass before in-place mode ships. |
| GOAL-003 | Operate locally and privately | Perform analysis and transformation offline without uploading photographs or creating identity profiles. | Network-isolation tests pass; no biometric identity feature or default telemetry exists. |
| GOAL-004 | Make automation auditable | Explain every decision with stable evidence, confidence, policy, model, and file provenance. | Every planned action has a schema-valid record and can be reproduced from the job manifest. |
| GOAL-005 | Scale predictably | Process collections from one image to hundreds of thousands without unbounded memory growth or fragile shell behavior. | Soak, cancellation, restart, and bounded-resource benchmarks pass. |
| GOAL-006 | Support professional operations | Provide stable CLI contracts, machine-readable output, deterministic exit codes, resumable jobs, review queues, and diagnostics. | CLI contract and compatibility tests pass across supported Windows environments. |
| GOAL-007 | Remain maintainable and extensible | Separate codecs, metadata, inference, policy, persistence, and presentation behind tested contracts. | Architecture conformance tests prevent forbidden dependencies and substitute test doubles at each port. |
| GOAL-008 | Earn trust through abstention | Prefer REVIEW or NO-OP over an uncertain or unsafe rotation. | False-rotation ceiling is met before optimizing coverage. |

## Success hierarchy

1. No source loss or silent corruption.
2. Extremely low false-rotation rate.
3. Clear auditability and recoverability.
4. Useful accepted coverage.
5. Competitive throughput and usability.

Throughput or coverage may never be optimized by weakening levels 1–3 without a new approved product decision.

## Primary persona

A Windows user with thousands of personal photos, most captured as selfies or portraits, who is comfortable running a guided CLI but does not want to inspect every file. Secondary personas are developers and digital archivists who need deterministic batch/report contracts.

## Business/product constraints

- Local-first, no required account or cloud service.
- Windows-first distribution and support.
- A professional CLI is the primary product surface.
- The application must support unattended automation while retaining safe defaults.
- Development shall be evidence-driven; no accuracy or fidelity guarantee is marketed before passing the defined corpus and fault gates.
