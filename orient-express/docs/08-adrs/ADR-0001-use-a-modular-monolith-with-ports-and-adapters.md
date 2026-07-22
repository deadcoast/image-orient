---
document_id: ADR-0001
title: Use a modular monolith with ports and adapters
version: 0.1.0
status: accepted
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0001]
---

# ADR-0001: Use a modular monolith with ports and adapters

**Status:** Accepted  
**Decision date:** 2026-07-21

## Context

The product is a local batch processor with significant internal complexity but no inherent need for network services. A script-only structure would entangle CLI, native tools, ML policy, and file mutation; microservices would multiply deployment and transaction problems.

## Decision

Build one deployable CLI as a modular monolith. Separate Domain, Application, Contracts, Infrastructure adapters, and Presentation. Enforce dependency direction with architecture tests. Child processes are supervised adapters, not services.

## Alternatives considered

A Python script bundle was rejected as the production architecture because packaging, Windows atomic operations, typed contracts, and long-term boundary enforcement would be weaker. Microservices were rejected because offline single-user operation gains no value from distributed failure modes.

## Consequences

Simple deployment and in-process orchestration; adapters remain replaceable. The team must actively enforce boundaries to avoid a monolith becoming coupled. Long-running work is job-oriented rather than daemon-oriented.

## Proof / review gate

Architecture tests in WP-001 and a vertical diagnostic slice must demonstrate adapter replacement with fakes.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
