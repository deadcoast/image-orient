# WP-010 context packet — transform engine

Generated 2026-07-24 per DOC-DEVPLAN ("Work-package context packet").

## Objective

The engine that physically fixes a photo: compose the lossless DCT-domain
JPEG operation for a policy-authored plan, execute it into an exclusive
same-volume stage file, and set the orientation tag to 1 on the stage copy
(PRD-FR-011, PRD-FR-012, NFR-FID-002). This work package delivers the
engine and its ports; wiring it into `scan`/`apply` arrives with the
pipeline work packages (WP-011..WP-014).

## Dependencies (all satisfied)

- SPIKE-004 (complete): metadata writes go through the ExifTool adapter on
  the stage copy (`-P -overwrite_original`); jpegtran never manages markers
  beyond `-copy all`.
- SPIKE-005 (complete): edge-policy mapping is normative — `abort` =
  `-perfect` with `JPEG_EDGE_NOT_PERFECT` refusal; `preserve-partial` =
  default mode; `trim` = `-trim`; `reencode` = raster path. Losslessness is
  proven by inverse-transform round-trip, never decoded-pixel comparison.
  Evidence matrix: 512x512 always accepted by `-perfect`; 500x500 always
  refused.
- SPIKE-007 (complete): capability truth-telling patterns; jpegtran
  availability surfaces through doctor.
- WP-005 (implemented): `IMetadataService.WriteOrientationAsync` exists and
  is SPIKE-004-proven.

## Contracts delivered

- `OrientExpress.Contracts.Transform`: `JpegOperation` (the eight lossless
  operations), `JpegEdgePolicy`, `TransformPlan` (policy-authored, executed
  verbatim — the transformer can never pick a fallback silently),
  `StagingTarget`, `TransformArtifact` (`CoefficientLossless` is true only
  for `-perfect` semantics or untouched coefficients).
- `OrientExpress.Application.Transform`: `IImageTransformer` port
  (DOC-COMPONENTS shape); `OrientationNormalizationPlanner` — the pure
  deterministic mapping EXIF orientation 1..8 → one lossless operation +
  expected dimensions + tag reset to 1 (AC-006 ordering: display transform
  first).
- `OrientExpress.Infrastructure.Imaging`: `JpegtranLocator` (DEP-008,
  doctor-visible digest), `JpegtranArguments` (pure builder; `-copy all`
  always), `JpegtranTransformer` (stage-exclusive creation per DOC-FSSAFE,
  typed errors: `TRANSFORM_INPUT_NOT_JPEG`, `TRANSFORM_STAGE_CONFLICT`,
  `TRANSFORM_TOOL_MISSING`, `TRANSFORM_TOOL_FAILED`,
  `JPEG_EDGE_NOT_PERFECT`, `TRANSFORM_REENCODE_UNAVAILABLE`; stage cleanup
  on every failure path).

## Acceptance criteria addressed

- AC-021: eligible rotations use the DCT path; byte-exact inverse-rotation
  round-trip proven in tests against the real tool.
- AC-022: all four edge policies follow the SPIKE-005 mapping and are never
  silent; `reencode` is a typed refusal until a raster encoder exists.
- AC-023 (engine level): the transformer produces upright pixels and sets
  the tag to 1 on the stage copy; end-to-end display-equivalence
  verification is WP-013's independent reopen.

## Honest scope

- The raster re-encode path does not exist; `jpegEdge=reencode` refuses
  with `TRANSFORM_REENCODE_UNAVAILABLE`.
- AC-024 (metadata round-trip corpus) stays gated on the SPIKE-001 corpus
  tranche.
- The CLI still cannot rotate a photo: `scan`/`apply` remain gated until
  the pipeline work packages wire this engine to discovery, the job store,
  and verification.
