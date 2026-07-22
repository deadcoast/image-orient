---
document_id: DOC-METADATA
title: Metadata, Orientation Semantics, Codecs, and Fidelity
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Media I/O
depends_on: [DOC-ARCH, DOC-FSSAFE, ADR-0006, ADR-0007]
traces_to: [PRD-FR-003, PRD-FR-011, PRD-FR-012, PRD-FR-013, PRD-FR-021, NFR-FID-001, NFR-FID-002]
---

# Metadata and codecs

## EXIF Orientation semantics

The tag represents a transform from stored pixels to intended display. Values include mirrored cases:

| Value | Display transform description |
|---:|---|
| 1 | identity |
| 2 | mirror horizontal |
| 3 | rotate 180° |
| 4 | mirror vertical |
| 5 | transpose |
| 6 | rotate 90° clockwise |
| 7 | transverse |
| 8 | rotate 270° clockwise |

The implementation uses a dihedral transform type, not an integer-degree shortcut, because values 2/4/5/7 include reflection.

## Transform composition

Let:

- `E` = metadata display transform;
- `R` = visually inferred additional quarter-turn after displaying with `E`;
- `N = R ∘ E` = physical transform applied to stored pixels.

After successful physical normalization:

- output pixels equal `N(source pixels)`;
- output Orientation is 1;
- expected dimensions follow `N`;
- metadata dimensions/thumbnails are updated or regenerated according to policy.

The engine does **not** infer whether the user prefers a mirrored versus non-mirrored selfie. It honors the metadata mirror state and only infers the rotational component in v1.

## Metadata snapshots

Read raw numeric values and group-qualified names. Snapshot categories:

- orientation and pixel dimensions;
- capture timestamps and timezone offsets;
- camera/device/lens/exposure;
- GPS;
- author/copyright/rights;
- XMP/IPTC descriptive data;
- color profile;
- embedded thumbnail/preview;
- maker notes;
- application edit history;
- file-system timestamps/attributes separately.

Do not promise byte-for-byte segment preservation unless a format/path proves it.

## Preservation classes

| Class | Behavior |
|---|---|
| `RequiredExact` | Value must survive semantically; mismatch blocks commit. |
| `RequiredNormalized` | Value may be rewritten to canonical equivalent; mismatch blocks commit. |
| `BestEffort` | Attempt preservation; differences are reported. |
| `Regenerated` | Derived dimension/orientation/thumbnail fields may be intentionally rebuilt. |
| `RemovedByPolicy` | Privacy/security policy intentionally strips the field. |
| `Unsupported` | Adapter cannot preserve safely; planning refuses that policy/format. |

Default personal-photo policy should preserve location and personal metadata rather than stripping it, because data removal is also destructive. A separate explicit privacy profile may strip selected fields.

## ExifTool adapter

- Bundle or locate a digest-approved executable and companion files.
- Probe version/capabilities during `doctor`.
- Start with `-stay_open True -@ -`.
- Use unique numeric `-execute<ID>` markers for framing.
- Construct arguments from typed allowlists; never concatenate shell commands.
- Use standard streams directly with no shell.
- Bound response bytes and duration.
- Serialize access per ExifTool process; pool a small configured number if proven safe.
- Capture warnings/errors and map to stable reason codes.
- Restart/quarantine after framing failure, timeout, or unexpected exit.
- Record exact executable digest/version in job provenance.

## Baseline codec matrix

| Format | Read/analyze | Transform/write | Metadata | Fidelity policy |
|---|---|---|---|---|
| JPEG | Must | Must | Must | DCT lossless for eligible quarter-turns; explicit fallback |
| PNG | Must | Must | Must for governed fields | Lossless raster encode; chunk/profile verification |
| TIFF | Probe/Should | Capability-gated | Capability-gated | Multipage, compression, depth, metadata complexity may refuse |
| WebP | Probe/Should | Capability-gated | Capability-gated | Lossless/lossy source mode and metadata verified |
| HEIC/HEIF | Optional | Optional | Optional | Requires exact decoder/encoder/metadata capability and licensing |
| AVIF | Optional | Optional | Optional | Same operation-level gate |
| RAW | Report only | No | Read-only optional | Out of mutation scope |
| Animated/multipage | Report | Default refuse | Read-only | No implicit frame/page loss |

`doctor --json` reports each operation independently.

## JPEG transform policy

For a requested quarter-turn, inspect sampling geometry and transform eligibility before writing.

Proposed policy enum:

- `abort`: refuse when perfect lossless transform cannot include all source blocks.
- `preserve-partial`: use qualified library behavior that retains partial edge MCUs where supported.
- `trim`: discard non-transformable edge blocks; must be explicit and report changed dimensions/cropped pixels.
- `reencode`: decode and encode with explicit quality/subsampling/profile; must be explicit and report generation change.

The default remains provisional until SPIKE-005. No policy is selected implicitly because another mode failed.

Use marker-copy behavior suitable for the metadata strategy. Avoid independently copying stale EXIF dimension/orientation fields after transform.

## Raster encode policy

Raster path records:

- output format;
- bit depth/pixel format;
- alpha behavior;
- color profile and rendering intent;
- compression/quality/subsampling;
- metadata profile;
- deterministic encoder options where available.

For JPEG re-encode fallback, never infer “same quality” from quantization tables without documenting limitations.

## Independent verification

Reopen with a verifier configuration distinct from the writer when practical. Check:

- decodable format and expected frame count;
- expected dimensions/bit depth/alpha;
- Orientation=1 and expected display geometry;
- required metadata diff;
- color profile presence/digest;
- JPEG lossless-equivalence criteria for DCT path;
- no unexpected crop or output collision;
- semantic thumbnail orientation where guaranteed.

## Sidecars and companions

XMP sidecars, Live Photo/Motion Photo companions, RAW+JPEG pairs, and catalog databases are not automatically mutated in v1. Discovery may identify/report them. A future companion policy must be explicit and transactional.
