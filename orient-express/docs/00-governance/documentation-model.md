---
document_id: DOC-GOVMODEL
title: Documentation Model and Governance
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: []
traces_to: [GOAL-004, GOAL-007]
---

# Documentation model

## Purpose

The documentation is an executable design control system. It prevents context loss by linking intent to implementation and proof rather than relying on a single long specification.

## Three dependency planes

```text
WHY: GOAL -> product requirement -> non-functional constraint
WHAT: requirement -> architecture/component/schema/ADR
HOW: component/requirement -> spike -> work package -> milestone -> test/evidence
```

A dependency arrow points from prerequisite to dependent. A trace link means “provides evidence or realization for” and may cross planes.

## Authoritative order

When artifacts conflict, resolve in this order:

1. Accepted safety/privacy/legal constraints.
2. Normative requirements and acceptance criteria.
3. Accepted ADRs.
4. External schemas and CLI compatibility policy.
5. Architecture detail.
6. Work plans.
7. Research notes and examples.

A conflict is never silently reconciled in code; it requires a documentation change.

## Document states

Draft → Provisional → Baseline → Implemented → Verified. Superseded documents remain addressable and name their replacement.

## Change impact procedure

For every proposed change:

1. Locate the initiating goal/requirement.
2. Traverse downstream edges in `planning/traceability-matrix.csv` and `planning/dependency-register.csv`.
3. Review affected ADRs, schemas, components, work packages, tests, risks, and release gates.
4. Update all affected artifacts in one review.
5. Run `tools/validate_docs.py` and regenerate graphs.
6. Attach generated evidence to the implementing pull request/release record.

## Context packets

A work package begins with a generated context packet containing its mapped requirements, components, ADRs, risks, tests, upstream spike results, schemas, and acceptance gates. This is how implementation remains contextual without copying entire documents into tickets.

## Validation policy

CI fails on duplicate/unknown IDs, broken local links, malformed structured files, unknown document dependencies, missing Must-requirement test mappings, or dependency cycles in the implementation work graph.
