---
document_id: ADR-0008
title: Use SQLite for durable job state
version: 0.1.0
status: provisional
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0008]
---

# ADR-0008: Use SQLite for durable job state

**Status:** Accepted pending scale spike  
**Decision date:** 2026-07-21

## Context

Jobs must survive crashes, support millions of rows/events, seal plans, query review items, migrate schemas, and operate without a service.

## Decision

Use SQLite via Microsoft.Data.Sqlite with a single logical writer, transactions, append-only events, integrity commands, and evaluated journal mode.

## Alternatives considered

Loose JSON files are hard to update atomically/query at scale. An embedded document DB adds a less-standard dependency. A server database violates simple offline deployment.

## Consequences

Schema/migration discipline and WAL/network-filesystem policy are required. Job DB is canonical; exports are contracts, not storage.

## Proof / review gate

SPIKE-008 at 100k and 1m synthetic rows plus crash/migration/integrity tests.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
