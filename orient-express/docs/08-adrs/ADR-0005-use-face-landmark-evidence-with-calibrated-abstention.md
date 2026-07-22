---
document_id: ADR-0005
title: Use face-landmark evidence with calibrated abstention
version: 0.1.0
status: provisional
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0005]
---

# ADR-0005: Use face-landmark evidence with calibrated abstention

**Status:** Accepted as baseline hypothesis  
**Decision date:** 2026-07-21

## Context

Most target photos are selfies. Upright facial geometry is interpretable, but no model can safely classify every image and intentional sideways poses exist.

## Decision

Evaluate all four rotations using a compact face detector with five landmarks, derive interpretable features, calibrate probabilities/risk, and abstain to REVIEW when gates fail.

## Alternatives considered

Aspect ratio and EXIF-only logic are insufficient. A single opaque scene classifier may be a fallback but not the unproven primary path. Forced argmax was rejected.

## Consequences

Four inference passes increase cost; thumbnails/provider acceleration mitigate it. Corpus/calibration quality becomes critical. No face recognition or identity profiles are created.

## Proof / review gate

SPIKE-001/002 and the governed evaluation gates.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
