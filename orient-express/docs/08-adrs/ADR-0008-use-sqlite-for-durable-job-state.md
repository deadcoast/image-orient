---
document_id: ADR-0008
title: Use SQLite for durable job state
version: 0.1.1
status: accepted
as_of: 2026-07-23
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0008]
---

# ADR-0008: Use SQLite for durable job state

**Status:** Accepted (SPIKE-008 proof gate met 2026-07-23)  
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

**Evidence (2026-07-23,
[report](../../spikes/SPIKE-008-job-store/report.md)):** gate met at both row
counts with wide margins — batched inserts 162k–185k rows/s (target 20k),
1m-row restart+counts 43 ms (target 2 s), migration 243 ms (target 30 s),
`integrity_check = ok` including after kernel-kill crash injection with
exactly the committed rows surviving. DOC-DATAMODEL constraints (append-only
events, sealed-plan immutability, forward-only journal checkpoints) are
enforced by schema triggers and proven by CI tests. Single-row transactions
measured 6.5× slower than 1k-row batches: the batched single-writer queue is
a requirement, not an optimization. Supply chain: the transitive
`SQLitePCLRaw.lib.e_sqlite3` 2.1.11 is vulnerable (GHSA-2m69-gcr7-jv3q);
`SQLitePCLRaw.bundle_e_sqlite3` is pinned at ≥ 2.1.12 (DEP-018).

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
