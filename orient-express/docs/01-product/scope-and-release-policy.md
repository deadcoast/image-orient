---
document_id: DOC-SCOPE
title: Scope and Release Policy
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Product
depends_on: [DOC-CHARTER, DOC-RESEARCH]
traces_to: [PRD-FR-021, NFR-COMP-001, NFR-COMP-002]
---

# Scope

## Baseline formats

- **JPEG/JFIF/Exif:** Must read, infer, losslessly quarter-turn when eligible, normalize metadata, and verify.
- **PNG:** Must read, infer, raster-transform when needed, preserve governed metadata, and verify.
- **TIFF/WebP:** Candidate post-baseline operations, gated by codec and metadata matrix.
- **HEIC/HEIF/AVIF:** Optional capability-gated support; no blanket promise.
- **RAW/video/animated images/PDF:** Out of v1 mutation scope. They may be reported as unsupported.

## Filesystem support

Primary guarantee target: local NTFS on supported Windows 11 x64. ReFS, SMB/network shares, FAT/exFAT, removable media, OneDrive/cloud placeholders, reparse points, hardlinks, encrypted/compressed files, and alternate data streams receive explicit capability behavior and may be safe-copy-only or unsupported.

## Release tiers

| Tier | Content |
|---|---|
| Alpha | Diagnostic/read-only spikes and scan planner; no in-place guarantee. |
| Beta | Safe-copy end-to-end with verification and documented limitations. |
| Release candidate | Guarded in-place, recovery, undo, quality/security/performance gates, signed artifacts. |
| v1 | All Must requirements traced to passing evidence; no critical unaccepted risk. |

## Compatibility promise

Patch releases preserve v1 machine contracts unless fixing a security/safety defect makes that impossible. Additive JSON fields are allowed only where schemas explicitly permit them. Breaking CLI/schema/job/model changes require a new contract major version and migration/diagnostic path.

## Deferred features

A desktop review UI, cross-platform packaging, cloud sync, photo catalog integration, distributed workers, arbitrary plugin marketplace, and user-trained models are deferred until v1 evidence demonstrates demand and safety.
