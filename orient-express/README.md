---
document_id: DOC-INDEX
title: orient-express Development Specification Index
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: []
traces_to: [GOAL-001, GOAL-002, GOAL-003, GOAL-004, GOAL-005, GOAL-006, GOAL-007, GOAL-008]
---

# orient-express development specification

**Baseline:** 0.1.0  
**As of:** 2026-07-21  
**Product:** Windows-first, offline, professional CLI for automatic upright photo orientation  
**Provisional executable:** `upright`

This repository is the authoritative planning baseline for developing orient-express. It deliberately separates product intent, normative requirements, architecture, verification, delivery, and evidence. Source code is **not** represented as complete by this package.

## Start here

1. [Master specification](MASTER-SPEC.md) — concise integrated baseline and release gates.
2. [Product charter](docs/01-product/product-charter.md) — problem, outcomes, principles, and non-goals.
3. [Product requirements](docs/01-product/product-requirements.md) and [system requirements](docs/03-requirements/system-requirements.md).
4. [System architecture](docs/04-architecture/system-architecture.md), [processing pipeline](docs/04-architecture/processing-pipeline.md), and [filesystem safety](docs/04-architecture/filesystem-safety.md).
5. [ML orientation engine](docs/04-architecture/ml-orientation-engine.md) and [corpus/evaluation plan](docs/05-quality/corpus-and-evaluation-plan.md).
6. [Development plan](docs/06-delivery/development-plan.md), [technical spikes](docs/06-delivery/technical-spikes.md), and [work breakdown](docs/06-delivery/work-breakdown-structure.md).
7. [Traceability guide](docs/07-traceability/traceability-guide.md) and generated graphs.

## Baseline decisions

- Build a **modular monolith** in C# on current .NET 10 LTS, targeting supported Windows 11 x64.
- Run image analysis locally. No cloud dependency, face recognition, identity embeddings, or default telemetry.
- Treat EXIF display orientation and visual upright orientation as distinct transforms.
- Use a four-candidate ONNX face/landmark pipeline as the baseline hypothesis, calibrated to abstain.
- Make `scan` read-only and make `apply` consume an immutable, fingerprinted job plan.
- Default to safe-copy output. Guard in-place changes with verified backup, journal, verification, and atomic replace.
- Prefer DCT-domain JPEG quarter-turns; never hide trim, edge loss, or re-encoding.
- Prove the high-risk assumptions with technical spikes before downstream implementation depends on them.

## Documentation layers

| Layer | Question answered | Primary artifacts |
|---|---|---|
| Product | Why does the product exist and what must users achieve? | Charter, workflows, PRD, scope |
| Requirements | What behavior and qualities are normative? | SRS, NFRs, acceptance criteria, schemas |
| Architecture | How is responsibility divided and how do components interact? | Architecture, contracts, pipeline, safety, ML, CLI |
| Quality | What evidence proves fitness? | Test, corpus, performance, security, compatibility |
| Delivery | In what dependency order will the system be proven and built? | Spikes, WBS, milestones, risks, release gates |
| Traceability | How are goals, requirements, components, work, tests, and evidence connected? | CSV catalogs, matrices, graphs, validator |

## Machine-readable planning assets

The [`planning/`](planning/) directory contains requirement, traceability, test, risk, milestone, decision, source, backlog, and dependency catalogs. The [`schemas/`](schemas/) directory contains draft external contracts. Run:

```powershell
pwsh ./tools/validate-docs.ps1
```

The validator rejects broken local links, unknown document dependencies, malformed JSON/CSV, duplicate IDs, unknown traceability references, and orphaned Must requirements.

## Status vocabulary

- **Draft** — exploratory and not yet approved.
- **Provisional** — selected direction with an unresolved proof gate.
- **Baseline** — approved planning input; implementation may proceed within its constraints.
- **Superseded** — retained for history but not governing.
- **Implemented** — code exists and its mapped tests pass.
- **Verified** — release evidence satisfies the documented gate.

Current status is summarized in [STATUS.md](STATUS.md).
