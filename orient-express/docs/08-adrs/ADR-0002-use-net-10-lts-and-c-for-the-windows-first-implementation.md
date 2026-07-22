---
document_id: ADR-0002
title: Use .NET 10 LTS and C# for the Windows-first implementation
version: 0.1.0
status: accepted
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0002]
---

# ADR-0002: Use .NET 10 LTS and C# for the Windows-first implementation

**Status:** Accepted with kickoff revalidation  
**Decision date:** 2026-07-21

## Context

The application is Windows-first, needs strong filesystem/handle APIs, local database and ONNX integration, a professional CLI, and self-contained distribution.

## Decision

Use C# on the current supported .NET 10 LTS line and target `win-x64` initially. Pin the exact supported SDK/runtime patch at implementation and release. Do not promise NativeAOT.

## Alternatives considered

Python remains useful for research tooling but not the primary deployable host. Rust/C++ could provide tighter native control but would increase implementation and interop cost for CLI, JSON, SQLite, and model orchestration.

## Consequences

Strong Windows API access, typed async/concurrency, testing, and packaging. Self-contained size is accepted. Native dependencies still require explicit distribution/security work.

## Proof / review gate

SPIKE-003 and SPIKE-010 must validate runtime/provider and clean-VM packaging.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
