---
document_id: DOC-TRACEGUIDE
title: Traceability Guide
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-GOVMODEL]
traces_to: [GOAL-004, GOAL-007]
---

# Traceability guide

## Why a graph, not a single specification

A large design becomes stale when decisions, requirements, code work, and tests are copied into disconnected documents. PhotoUpright uses stable IDs and two related graphs:

- **Documentation dependency graph:** which document must be read/updated before another.
- **Implementation dependency graph:** which spike/component/work package must exist or pass before another.

The traceability matrix connects product intent to realization and proof.

## ID namespaces

| Prefix | Meaning |
|---|---|
| `GOAL-` | Product outcome |
| `PRD-FR-` | Functional product requirement |
| `NFR-` | Non-functional requirement |
| `AC-` | Acceptance criterion |
| `COMP-` | Architecture component |
| `ADR-` | Architecture decision |
| `SPIKE-` | Decision experiment/gate |
| `WP-` | Work package |
| `M` | Milestone |
| `TEST-` | Test/evidence case |
| `RISK-` | Risk |
| `SRC-` | Research source |
| `DOC-` | Document |

## Required trace chain

Every Must requirement should have:

```text
GOAL
  -> PRD-FR / NFR
  -> AC
  -> COMP and/or schema/ADR
  -> WP
  -> TEST
  -> release evidence
```

Some NFRs map directly to tests and multiple components/work packages.

## Canonical files

- [`requirements-catalog.csv`](../../planning/requirements-catalog.csv)
- [`traceability-matrix.csv`](../../planning/traceability-matrix.csv)
- [`test-matrix.csv`](../../planning/test-matrix.csv)
- [`backlog.csv`](../../planning/backlog.csv)
- [`dependency-register.csv`](../../planning/dependency-register.csv)
- [`decision-register.csv`](../../planning/decision-register.csv)

Markdown tables are readable views; CSV/schema artifacts are canonical for automation.

## Dependency semantics

- `depends_on` in document front matter means prerequisite context.
- implementation edge `A -> B` means B depends on A.
- trace edge `A -> B` means B realizes/proves/derives from A according to edge type.
- feedback loops must not be encoded as hard dependency cycles; they are review triggers recorded separately.

## Change impact example

Changing the default JPEG edge policy:

1. Locate `PRD-FR-011`, `NFR-FID-002`, AC-021/022.
2. Follow to `COMP-TRANSFORM`, `SPIKE-005`, `WP-010`, media/fault tests.
3. Update ADR-0007, config schema, CLI option semantics, job/plan representation, verifier, compatibility, risks, release notes.
4. Regenerate graphs and run validator.
5. Execute regression/evidence suite.

## CI checks

- unique IDs;
- all references known;
- no documentation or delivery dependency cycles;
- all Must requirements have acceptance, component, WP, and test mappings;
- all tests map to a requirement or acceptance criterion;
- all WPs have a milestone and dependency closure;
- all provisional ADRs name proof gates;
- all local document links resolve.

## Evidence linkage

The planning matrix names expected evidence paths. Production CI should emit an evidence manifest that maps test IDs to immutable artifact URLs/digests and build/release ID. That manifest—not a green UI badge—is the auditable release proof.
