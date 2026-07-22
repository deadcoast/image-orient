---
document_id: ADR-0003
title: Separate scan plans from apply
version: 0.1.0
status: accepted
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0003]
---

# ADR-0003: Separate scan plans from apply

**Status:** Accepted  
**Decision date:** 2026-07-21

## Context

A one-pass discover/decide/write loop cannot support review, stale-source detection, reproducibility, safe automation, or robust recovery.

## Decision

Make `scan` strictly read-only and persist an immutable fingerprinted plan. `apply` revalidates and executes only plan actions. Review decisions are append-only overlays.

## Alternatives considered

A dry-run flag on a one-pass command was rejected because a later run may discover different files/versions/policies. Keeping an in-memory plan was rejected for large jobs and restart.

## Consequences

More storage and commands, but much stronger auditability, review, resume, and safety. Job/schema compatibility becomes a first-class contract.

## Proof / review gate

AC-012 through AC-015, job seal tests, and stale-plan E2E tests.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
