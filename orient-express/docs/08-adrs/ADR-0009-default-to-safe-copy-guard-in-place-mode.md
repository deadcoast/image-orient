---
document_id: ADR-0009
title: Default to safe-copy; guard in-place mode
version: 0.1.0
status: accepted
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0009]
---

# ADR-0009: Default to safe-copy; guard in-place mode

**Status:** Accepted  
**Decision date:** 2026-07-21

## Context

Orientation changes touch irreplaceable personal files. Users need convenience, but destructive defaults would be unacceptable.

## Decision

Default to separate-output safe-copy. In-place requires explicit acknowledgement, supported filesystem, strong fingerprint, verified backup, durable journal, independent output verification, atomic replacement, recovery, and undo.

## Alternatives considered

Direct overwrite and generic temporary rename were rejected. Making in-place default was rejected. Permanent copy-only would be safest but would not satisfy cleanup workflows.

## Consequences

Additional disk use and workflow steps; significantly stronger trust. In-place can be disabled independently if its proof fails.

## Proof / review gate

SPIKE-006 and zero-source-loss fault suite.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
