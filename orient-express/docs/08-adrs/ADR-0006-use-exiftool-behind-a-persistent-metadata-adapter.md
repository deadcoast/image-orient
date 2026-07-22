---
document_id: ADR-0006
title: Use ExifTool behind a persistent metadata adapter
version: 0.1.0
status: provisional
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0006]
---

# ADR-0006: Use ExifTool behind a persistent metadata adapter

**Status:** Provisional pending license/round-trip gate  
**Decision date:** 2026-07-21

## Context

Image metadata is heterogeneous and generic encoders often lose or normalize fields. Broad read/write coverage is needed, especially for personal photos.

## Decision

Use ExifTool through a typed persistent `-stay_open` subprocess adapter provisionally. Preserve fields according to an explicit semantic policy and verify before commit.

## Alternatives considered

A pure managed metadata library plus WIC writer may reduce subprocess packaging but likely lacks equivalent write breadth. It remains a substitution path if round-trip/licensing fails.

## Consequences

Adds executable/protocol/license/security supervision. Avoids reimplementing decades of metadata formats. The adapter cannot receive arbitrary command strings.

## Proof / review gate

SPIKE-004 and dependency/legal review. Failure blocks adoption, not the architecture.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
