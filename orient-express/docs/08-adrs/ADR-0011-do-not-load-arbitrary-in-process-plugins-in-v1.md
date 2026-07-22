---
document_id: ADR-0011
title: Do not load arbitrary in-process plugins in v1
version: 0.1.0
status: accepted
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0011]
---

# ADR-0011: Do not load arbitrary in-process plugins in v1

**Status:** Accepted  
**Decision date:** 2026-07-21

## Context

A plugin marketplace is not required for v1, while image/native/model processing and file mutation create high trust requirements.

## Decision

Provide internal first-party ports but no arbitrary in-process third-party plugin loading. Consider a constrained external-process protocol only after demand/evidence.

## Alternatives considered

MEF/reflection assembly loading and native plugin directories were rejected due to ABI, crash, privilege, DLL search, and supply-chain risk.

## Consequences

Third parties cannot extend v1 dynamically; maintainers can still replace adapters in source. Future isolation is easier to reason about.

## Proof / review gate

Architecture/security tests verify no plugin scanning/loading path.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
