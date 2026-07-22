---
document_id: DOC-DECISIONS
title: Architecture Decision Log
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-ARCH]
traces_to: [ADR-0001, ADR-0002, ADR-0003, ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0008, ADR-0009, ADR-0010, ADR-0011, ADR-0012]
---

# Decision register

| ID | Title | Status | Choice | Reason |
| --- | --- | --- | --- | --- |
| ADR-0001 | Use a modular monolith with ports and adapters | Accepted | One deployable CLI with strict internal boundaries; no distributed services. | Offline single-user workload benefits from simple deployment while retaining testability and replaceable integrations. |
| ADR-0002 | Use .NET 10 LTS and C# for the Windows-first implementation | Accepted with kickoff revalidation | Target current .NET 10 LTS, win-x64 self-contained deployment. | Long support window, strong Windows APIs, modern performance/tooling, and straightforward native interop. |
| ADR-0003 | Separate scan plans from apply | Accepted | Read-only scan seals immutable plan; apply validates and executes it. | Improves safety, auditability, review, resume, and automation. |
| ADR-0004 | Use ONNX behind a runtime abstraction | Accepted | Model contract is ONNX; Windows ML and direct ONNX Runtime are interchangeable adapters qualified by spike. | Local inference, hardware options, portability, and avoidance of framework lock-in. |
| ADR-0005 | Use face-landmark evidence with calibrated abstention | Accepted as baseline hypothesis | YuNet-class detector over four candidates plus geometric features, calibration, and REVIEW fallback. | Selfie-heavy domain provides strong interpretable evidence; precision matters more than forced coverage. |
| ADR-0006 | Use ExifTool behind a persistent metadata adapter | Provisional pending license/round-trip gate | ExifTool `-stay_open` protocol for metadata read/write; adapter is replaceable. | Broad mature metadata support and orientation semantics, while isolating process/licensing concerns. |
| ADR-0007 | Use lossless JPEG DCT transforms when eligible | Accepted pending edge-policy spike | libjpeg-turbo/jpegtran path for quarter turns; explicit MCU edge policy. | Avoids generational JPEG loss and preserves user originals more faithfully. |
| ADR-0008 | Use SQLite for durable job state | Accepted pending scale spike | Local SQLite store with append-only events, sealed plans, migrations, and integrity commands. | Transactional durability and queryability without a service dependency. |
| ADR-0009 | Default to safe-copy; guard in-place mode | Accepted | Separate output is default; in-place requires backup, journal, verification, acknowledgement, and atomic replace. | The irreversible-risk profile demands conservative defaults. |
| ADR-0010 | Version every external automation contract | Accepted | JSON Schemas, event versions, exit-code registry, config versions, model manifests, and compatibility rules. | Professional CLI automation requires predictable evolution. |
| ADR-0011 | Do not load arbitrary in-process plugins in v1 | Accepted | Internal ports only; future third-party extensibility, if needed, uses an isolated process protocol. | Reduces supply-chain, ABI, crash, and privilege risks while preserving future extensibility. |
| ADR-0012 | Use risk-first gated delivery | Accepted | Resolve corpus/ML, metadata, JPEG, filesystem, runtime, and packaging risks before broad implementation depends on them. | Prevents polished low-risk code from masking fundamental viability failures. |

Full records are in [`docs/08-adrs/`](../08-adrs/). Provisional decisions cannot unlock blocked production work until their named spikes pass.
