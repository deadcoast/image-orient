---
document_id: ADR-0010
title: Version every external automation contract
version: 0.1.0
status: accepted
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0010]
---

# ADR-0010: Version every external automation contract

**Status:** Accepted  
**Decision date:** 2026-07-21

## Context

Developers will automate the CLI and jobs may outlive a release. Unversioned output and mutable exit codes create operational breakage.

## Decision

Version CLI compatibility, JSON/JSONL, config, job manifest/database, model/review schemas, reason/exit codes, and migration rules.

## Alternatives considered

Best-effort backward compatibility without formal schemas was rejected. Human-text parsing was rejected.

## Consequences

More design/review overhead and golden fixtures. Enables safe automation and diagnostics. Breaking changes require major versions and migration.

## Proof / review gate

SPIKE-009, schema validation, prior-release compatibility suite.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
