---
document_id: ADR-0012
title: Use risk-first gated delivery
version: 0.1.0
status: accepted
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-RISKS]
traces_to: [ADR-0012]
---

# ADR-0012: Use risk-first gated delivery

**Status:** Accepted  
**Decision date:** 2026-07-21

## Context

The most dangerous unknowns are ML accuracy, metadata preservation, JPEG edge behavior, atomic filesystem recovery, provider deployment, and packaging—not command boilerplate.

## Decision

Use risk-first technical spikes with explicit pass/fail gates. Do not let downstream WPs depend on a provisional integration before its gate passes.

## Alternatives considered

Feature-first or UI-first delivery was rejected because it could create sunk cost around a nonviable core. A single long research phase was rejected in favor of executable evidence.

## Consequences

Early work may look less like a finished app, but architectural decisions are evidence-backed. The dependency graph and milestone reviews become governing artifacts.

## Proof / review gate

M0 requires all spikes to produce reproducible artifacts and updated ADRs.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
