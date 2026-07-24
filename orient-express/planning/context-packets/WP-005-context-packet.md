# WP-005 context packet — metadata and codec adapters

Generated 2026-07-23 per DOC-DEVPLAN ("Work-package context packet").

## Objective

Implement the diagnostic-mode metadata and format-probe foundation:
the production ExifTool `-stay_open` adapter behind the COMP-METADATA port,
a bounded managed format prober (COMP-CODEC probe surface), capability
truth-telling in `doctor`, and the `upright inspect <file>` alpha (M1 exit
deliverable) that reports identity, format, and EXIF orientation while
explicitly gating the decision engine.

## Mapped requirements and acceptance

- **PRD-FR-003** (Must): read EXIF Orientation 1–8 including mirrored values
  — AC-005 (diagnostic read path).
- **PRD-FR-012** (partial): the adapter exposes the orientation write proven
  by SPIKE-004; normalization sequencing itself is WP-010.
- **PRD-FR-021** (partial): JPEG/PNG baseline exposure; optional formats
  stay gated with stable reason codes until decode proof exists.
- **NFR-FID-001**: preservation is field-aware — the adapter's read/diff
  vocabulary is group-qualified tags, not byte-copy promises.

## Upstream spike conclusions (binding)

SPIKE-004: typed `-stay_open` protocol with `{ready<id>}` **and** `-echo4`
stderr sentinels; reads `-j -G1 -a -n`; writes `-P -overwrite_original`;
rewrite drops ADS and changes the file id (executor re-baselines).
SPIKE-007: capability by header/decode-attempt probing, never enumeration;
JPEG/PNG guaranteed; optional formats `CODEC_PRESENT_UNVERIFIED`/
`CODEC_NOT_INSTALLED`. SPIKE-006/WP-004: identity re-baseline vocabulary.

## Design constraints (DOC-ARCH layering)

- `OrientExpress.Contracts`: `MetadataSnapshot`/`MetadataDiff` and
  `ImageFormatProbe` records — package-free.
- `OrientExpress.Application`: `IMetadataService` (diagnostic subset of the
  DOC-COMPONENTS shape) and `IImageFormatProber` ports.
- `OrientExpress.Infrastructure.Metadata.ExifTool`: hardened stay-open
  client (thread-safe, lazy, disposal-clean), tool locator (PATH +
  LocalAppData install), digest capture, typed `Result<T>` error mapping.
- `OrientExpress.Infrastructure.Imaging`: bounded managed format prober
  (signatures, JPEG SOF dimensions + EXIF orientation both endiannesses,
  PNG IHDR); no pixel decode (WP-010).
- `OrientExpress.Cli`: composition; doctor + inspect wiring.

## Threat/risk notes

ExifTool consumes untrusted files: the adapter allowlists operations, never
concatenates caller strings into argument lines beyond values, and bounds
response reads. The prober reads a bounded prefix with share-friendly
handles and never follows reparse points (WP-004 pre-check). Tool discovery
prefers explicit config in later WPs; the located path, version, and digest
are always reported (doctor) so a tampered tool is visible.

## Test and evidence plan

InfrastructureTests: prober unit tests on synthetic byte fixtures (formats,
both TIFF endiannesses, truncation); ExifTool adapter integration tests that
`Assert.Skip` when the tool is absent (CI runners) and fully exercise
read/write/diff locally. CliTests: inspect contract (machine document shape,
gated decision block, missing-file refusal) tolerant of exiftool
presence/absence; doctor formats/metadata truth-telling both ways.

## Dependencies and consumers

No new packages (DEP-007/DEP-009 are external/OS). Consumers: WP-007
(orientation evidence), WP-010 (normalization), WP-013 (metadata verify),
WP-018 (support).

## Out of scope

Pixel decode and transforms (WP-010); metadata mutation beyond the
orientation write; preservation-policy enforcement engine (WP-010/WP-013);
sidecars; worker pools (WP-011); optional-format decode proof (corpus).

## Definition of done

Validator/build/format/tests green; doctor and inspect truth-tell with
stable reason codes; adapter provably survives tool absence with typed
errors; statuses, STATUS, CHANGELOG, manifest updated; traceability intact.
