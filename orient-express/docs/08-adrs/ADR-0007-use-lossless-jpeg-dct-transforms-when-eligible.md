---
document_id: ADR-0007
title: Use lossless JPEG DCT transforms when eligible
version: 0.1.1
status: accepted
as_of: 2026-07-23
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0007]
---

# ADR-0007: Use lossless JPEG DCT transforms when eligible

**Status:** Accepted pending edge-policy spike  
**Decision date:** 2026-07-21

## Context

Decoding and resaving JPEGs causes generational loss. Quarter-turns are often possible in the DCT domain, but partial MCU edges require explicit choices.

## Decision

Use libjpeg-turbo/jpegtran or equivalent qualified DCT transform for eligible JPEGs. Expose abort/preserve-partial/trim/reencode policy and never silently switch.

## Alternatives considered

Always raster re-encode was rejected for fidelity. Always trim was rejected as silent crop. Always abort may become the default if other modes are not safely predictable.

## Consequences

More complex capability/verification and native packaging; materially better fidelity. Metadata is orchestrated separately to avoid stale orientation/dimension fields.

## Proof / review gate

SPIKE-005 coefficient/pixel/dimension/marker matrix.

**Evidence (2026-07-23, [report](../../spikes/SPIKE-005-jpeg-edge/report.md)):**
SPIKE-005 proof gate met on libjpeg-turbo 3.2.0 with a 108-case adversarial
matrix. `-perfect` successes are coefficient-lossless (round-trip proven);
default mode is reversible with imperfect edges; `-trim` visibly resizes;
no mode silently crops or re-encodes. Policy mapping to `output.jpegEdge`
approved with `abort` as default. Verification must use inverse-transform
round-trip or coefficient digests, never decoded-pixel comparison against a
spatial reference (integer IDCT and chroma upsampling are not
rotation-equivariant; deltas <=4 are expected).

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
