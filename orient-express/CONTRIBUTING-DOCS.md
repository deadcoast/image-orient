---
document_id: DOC-CONTRIB
title: Contributing to the Development Specification
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-GOVMODEL, DOC-TRACEGUIDE]
traces_to: []
---

# Contributing to the specification

## Required change discipline

A change is incomplete when it updates prose but not its traceability. The author must:

1. Identify affected IDs.
2. Update the normative requirement or explicitly state that no requirement changes.
3. Add or supersede an ADR for architectural consequences.
4. Update acceptance criteria and test mappings.
5. Update schemas and compatibility notes for external contracts.
6. Update work packages, risks, and release gates when sequencing changes.
7. Run the documentation validator and regenerate graphs.

## Normative language

- **Shall / Must:** release-blocking obligation.
- **Should:** expected unless an approved deviation exists.
- **May / Could:** optional.
- **Will:** planned statement, not itself a requirement.
- **Target:** a proposed measurable threshold that must be confirmed by evidence.
- **Guarantee:** permitted only when a defined compatibility matrix and passing evidence exist.

## ID policy

IDs are permanent. Never recycle a deleted ID. Mark removed items `Superseded`, retain their history, and point to replacements.

## Review roles

- Product approves goals, scope, workflows, and user-facing policy.
- Architecture approves boundaries, contracts, ADRs, and dependency direction.
- ML/Quality approves corpus, labels, calibration, and release metrics.
- Security approves threat controls, supply chain, and privacy.
- Release approves packaging, notices, signing, installation, and rollback.
- A safety-critical change requires at least two domain approvals.
