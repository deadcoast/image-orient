---
document_id: DOC-CHANGELOG
title: Development Specification Changelog
version: 0.1.16
status: baseline
as_of: 2026-07-24
owner: Architecture
depends_on: []
traces_to: []
---

# Changelog

## 0.1.16 — 2026-07-24

WP-010 transform engine (implemented) and SPIKE-002 two-rule evaluation:

- Context packet at `planning/context-packets/WP-010-context-packet.md`.
- `OrientExpress.Contracts.Transform`: `JpegOperation` (the eight jpegtran lossless operations), `JpegEdgePolicy`, `TransformPlan` (policy-authored, executed verbatim — no silent fallbacks), `StagingTarget`, `TransformArtifact` (`CoefficientLossless` only for `-perfect` semantics or untouched coefficients).
- `OrientExpress.Application.Transform`: `IImageTransformer` port (DOC-COMPONENTS shape); `OrientationNormalizationPlanner` — pure mapping of EXIF orientation 1..8 to one lossless operation, expected dimensions, and tag reset to 1 (the deterministic no-ML normalization path; AC-006 display-transform ordering).
- `OrientExpress.Infrastructure.Imaging`: `JpegtranTransformer` per the SPIKE-005-approved mapping — always `-copy all`; `abort`→`-perfect` refusing `JPEG_EDGE_NOT_PERFECT`; `preserve-partial`→default; `trim`→`-trim`; `reencode`→typed `TRANSFORM_REENCODE_UNAVAILABLE` (no raster encoder exists; a silent lossy substitute is forbidden). Exclusive stage creation (DOC-FSSAFE), stage cleanup on every failure path, orientation tag set on the stage copy via the ExifTool adapter (SPIKE-004 binding). `JpegtranLocator` (DEP-008) surfaces path+digest through a new doctor `transform-jpegtran` component row.
- Tests: 18 pure planner/policy unit tests; 7 integration tests against real jpegtran (skip when absent) — byte-exact inverse-rotation round-trip on the SPIKE-005 512x512 always-accepted fixture (AC-021), `JPEG_EDGE_NOT_PERFECT` on the 500x500 always-refused fixture, preserve-partial/trim/reencode/no-op/conflict/cleanup paths (AC-022). 225 tests green.
- SPIKE-002 evaluate mode now scores two decision rules per photo (first real-corpus run: rule A 80.95% on 21 photos with confidently-wrong cases): rule B sums confident faces (>=0.6 vote filter), checks eyes-above-mouth geometry, requires a relative margin, and abstains with recorded reasons; `--show-failures` (console-only paths) and `--debug-evidence` added; results JSONs git-ignored.
- Honest scope: the CLI still cannot rotate a photo — the engine reaches `scan`/`apply` only through WP-009 + WP-011..WP-014; AC-023 is engine-level until WP-013's independent verification; AC-024 stays gated on the corpus tranche.
- Affected IDs: WP-010, SPIKE-002, PRD-FR-011, PRD-FR-012, NFR-FID-002, AC-006, AC-021, AC-022, AC-023, ADR-0006, ADR-0007, DEP-007, DEP-008, COMP-TRANSFORM, COMP-CODEC, COMP-METADATA, DOC-STATUS, DOC-CHANGELOG. No new packages; no schema text changes.

## 0.1.15 — 2026-07-24

WP-006 model registry and inference runtime (implemented) — M1 component roster complete:

- Context packet at `planning/context-packets/WP-006-context-packet.md`.
- `OrientExpress.Contracts`: manifest records per `model-manifest.schema.json` (permissive parse, code-validated required fields/enums), `RuntimeDescriptor`/`ModelHandle`/tensor IO records.
- `OrientExpress.Application`: `IModelRegistry` and `IInferenceRuntime` ports (DOC-COMPONENTS shapes).
- `OrientExpress.Infrastructure.Inference`: `FileModelRegistry` — bundles at `<modelRoot>/<id>/<version>/`, quarantine directory with recorded reasons for digest/schema/runtime failures (AC-038), atomic `active.json` pointer via temp+`File.Replace` with the prior model recorded, rollback restores only after re-verification (AC-039), active model refuses removal; `OnnxInferenceRuntime` — SPIKE-003 CPU baseline, session cache keyed by digest, artifact digest re-verified at load (DEP-006 rule in production).
- `upright models list|install|verify|activate|rollback|remove` implemented with stable error→exit mapping (digest → 32, manifest/runtime → 23, active/duplicate → 34); `rollback` recorded as an additive DOC-CLI 0.2.1 command per PRD-FR-020. Model root comes from the WP-003 effective configuration. Doctor: inference-runtime available; orientation-model shows the active bundle.
- Shipped `models/manifests/yunet-baseline.2023mar.json`; the qualified YuNet artifact installs, verifies, and activates end to end through the CLI.
- Tests: 6 registry integration tests (quarantine, tamper, atomic activate/rollback, active-remove refusal), 4 CLI lifecycle golden tests; 199 tests green.
- Honest scope: provider fallback beyond CPU is gated to WP-021; bundle signatures to WP-023; the decision engine (WP-007/008) remains gated on corpus evidence.
- Affected IDs: WP-006, PRD-FR-020, NFR-SEC-002, NFR-PRIV-001, AC-038, AC-039, ADR-0004, DEP-005, DEP-006, COMP-MODELS, COMP-INFERENCE, DOC-CLI, DOC-STATUS, DOC-CHANGELOG. No new packages; no schema text changes.

## 0.1.14 — 2026-07-23

SPIKE-003 inference runtime (complete; ADR-0004 accepted) and SPIKE-002 pilot (passing) — face detection runs locally:

- Pinned Microsoft.ML.OnnxRuntime 1.27.1 (DEP-005) centrally; pulled and digest-pinned the YuNet 2023mar model (232,589 B, sha256 `8f2383e4…`, MIT license recorded) and a US-government public-domain pilot portrait — artifacts cached locally, verified on every use, never committed (DEP-006 rule implemented).
- Shared harness under `spikes/SPIKE-002-orientation-proof/`: full managed YuNet stack — 640×640 BGR preprocessing, twelve-head decode (cls/obj/bbox/kps at strides 8/16/32) per OpenCV FaceDetectorYN, NMS, landmark mapping — plus 6 CI-safe math tests.
- SPIKE-003 evidence: model load 69 ms; warm inference 1.64 ms median / 1.96 ms p95; bitwise determinism within and across sessions; 15.5 MB native footprint; offline. CPU EP selected as v1 baseline; DirectML/Windows ML gated for WP-021.
- SPIKE-002 pilot: four-candidate approach recovered 4/4 rotations; eyes-above-mouth landmark check agreed 4/4. Key finding: inverted faces score 0.841 vs 0.947 upright (margin 0.106) — raw thresholds cannot decide orientation; ADR-0005's calibrated margin+abstention design is confirmed and its ADR carries the pilot note (stays provisional pending corpus precision).
- Findings recorded: the 2023mar export has a fixed 640×640 input; digest-mismatch refusal is enforced at artifact load.
- Affected IDs: SPIKE-003, SPIKE-002, ADR-0004, ADR-0005, DEP-005, DEP-006, DOC-WBS, DOC-STATUS, DOC-CHANGELOG, WP-006, WP-007, WP-008. New package: Microsoft.ML.OnnxRuntime 1.27.1 (inventoried). No schema or migration impact.

## 0.1.13 — 2026-07-23

WP-005 metadata and codec adapters (implemented, diagnostic mode):

- Context packet at `planning/context-packets/WP-005-context-packet.md`.
- `OrientExpress.Contracts`: `MetadataSnapshot` (group-qualified field-aware tags, NFR-FID-001) with EXIF orientation 1–8 accessor, `MetadataDiff`, `ImageFormatProbe`.
- `OrientExpress.Application`: `IMetadataService` (diagnostic subset of DOC-COMPONENTS: Identify/Read/WriteOrientation/Compare) and `IImageFormatProber` ports.
- `OrientExpress.Infrastructure.Metadata.ExifTool`: production SPIKE-004 protocol — lazy single-process `-stay_open`, per-command `{ready<id>}` + `-echo4` stderr sentinels, allowlisted operations, `-j -G1 -a -n` reads, `-P -overwrite_original` orientation write, volatile-group-aware field diff, tool locator + version/digest identity, clean degradation to `EXIFTOOL_NOT_FOUND`.
- `OrientExpress.Infrastructure.Imaging`: `ManagedFormatProber` — bounded 256 KiB header probe, content signatures (JPEG/PNG/HEIC/AVIF/WebP/TIFF/GIF/BMP), JPEG SOF dimensions + EXIF IFD0 orientation in both TIFF endiannesses, PNG IHDR; corrupt input warns, never throws (PRD-FR-003 read path, AC-005).
- `upright inspect <file>` alpha (M1 exit deliverable): identity + format + orientation with `decision.available=false` and `DECISION_ENGINE_GATED`; `inspect.result` machine document; missing file refused with typed error. Doctor truth-telling updated: metadata-exiftool reports version + digest when found, format-prober available, per-format abilities with reason codes; scan's gate list corrected (WP-004/005 satisfied).
- Tests: 9 prober tests (both endiannesses, truncation, typed not-found), 3 adapter tests including a real-tool read/write/diff cycle (skips when ExifTool is absent on CI), 4 inspect/doctor golden tests. 183 tests green across 10 test projects.
- Affected IDs: WP-005, PRD-FR-003, PRD-FR-012 (adapter portion), PRD-FR-021 (baseline exposure), NFR-FID-001, AC-005, ADR-0006, DEP-007, DEP-009, COMP-METADATA, COMP-CODEC, DOC-STATUS, DOC-CHANGELOG. No new packages; no schema or migration impact.

## 0.1.12 — 2026-07-23

SPIKE-007 format and codec capability matrix (complete):

- Harness under `spikes/SPIKE-007-codecs/` (the one Windows-TFM/WPF spike project): operational encode/decode/round-trip probes for JPEG/PNG/TIFF/BMP/GIF via WIC-backed WPF, a WIC metadata-query orientation read against an ExifTool-authored value, and header-engagement presence probes for optional formats.
- All safety expectations pass. Baseline matrix approved: JPEG/PNG guaranteed on WIC. This machine: HEIC/AVIF/WebP present-engaged; JPEG XL absent — reported with stable reason codes (`CODEC_PRESENT_UNVERIFIED` / `CODEC_NOT_INSTALLED`).
- Method findings (binding on WP-005): capability is determined by decode-attempt/header-engagement probes; registry Instance categories list only add-on codecs and managed COM enumeration returned empty — enumeration is not trusted.
- Licensing inventory recorded for WP-022: WIC has no redistribution burden; Store codec extensions are never bundled; Magick.NET remains a candidate behind a delegate-license review.
- DEP-009 selected; DEP-010 annotated; DOC-WBS 0.1.5 design note added.
- Affected IDs: SPIKE-007, DEP-009, DEP-010, DOC-WBS, DOC-STATUS, DOC-CHANGELOG, WP-005, WP-010, WP-022, NFR-COMP-001. No new packages; no schema or migration impact.

## 0.1.11 — 2026-07-23

SPIKE-004 metadata round-trip and ExifTool integration (complete for baseline formats); ADR-0006 accepted:

- Installed and digest-pinned ExifTool 13.59 (DEP-007, winget `OliverBetz.ExifTool` — exactly the baseline-research snapshot version); harness under `spikes/SPIKE-004-metadata/` with the typed `-stay_open` adapter prototype and 3 CI-safe fixture tests.
- 6/6 evidence scenarios pass: protocol synchronization on one persistent process; round-trip matrix (orientation write changes exactly `IFD0:Orientation` with EXIF 12 / GPS 5 / XMP 4 / IPTC 3 tags byte-stable and JPEG structure unchanged); `-P` timestamp preservation; error attribution via a per-command `-echo4` stderr sentinel (a protocol requirement discovered by this spike — without it stderr is unattributable); PNG XMP round-trip.
- Measured rewrite side effects: ExifTool drops ADS and changes the NTFS file id — same class as SPIKE-006's ReplaceFileW finding; metadata writes happen on the stage copy and fold into the commit re-baseline.
- Guarantee language recorded: maker notes explicitly non-guaranteed until the SPIKE-001 corpus tranche; ADS survival and file-id stability are executor concerns, not adapter guarantees.
- ADR-0006 0.1.1 provisional → **accepted**; DEP-007 selected with digest; DOC-WBS 0.1.4 adds WP-005/010/014 design notes.
- Affected IDs: SPIKE-004, ADR-0006, DEP-007, DOC-WBS, DOC-STATUS, DOC-CHANGELOG, WP-005, WP-010, WP-014, NFR-FID-001. No new packages; no schema or migration impact.

## 0.1.10 — 2026-07-23

SPIKE-005 JPEG lossless rotation edge policy (complete); ADR-0007 accepted:

- Installed and digest-pinned libjpeg-turbo 3.2.0 (DEP-008, winget `libjpeg-turbo.libjpeg-turbo.VC`); harness under `spikes/SPIKE-005-jpeg-edge/` runs a 108-case adversarial matrix (six dimension classes × 4:4:4/4:2:0 × three rotations × default/`-perfect`/`-trim`) on synthetic orientation-sensitive fixtures, with 5 CI tests proving the managed reference rotation.
- All safety expectations pass: `-perfect` successes (10) are coefficient-lossless by inverse-rotation round-trip; refusals (26) are characterized by rotation and alignment; default mode always produces full-dimension output and was round-trip lossless in all 36 cases on 3.2.0; `-trim` visibly resizes (23 cases); `-copy all`/`-copy none` marker behavior measured.
- **Binding verification finding (WP-013):** decoded-pixel comparison is the wrong losslessness test — integer IDCT and chroma upsampling are not rotation-equivariant (deltas ≤4, up to ~7% of bytes at 4:2:0, while coefficients are exact). Verification must use inverse-transform round-trip or coefficient digests.
- Approved `output.jpegEdge` policy mapping: `abort` (default) = `-perfect` + `JPEG_EDGE_NOT_PERFECT` refusal; `preserve-partial` = default mode with disclosure; `trim` = `-trim`; `reencode` = raster path. Always `-copy all`; metadata authority stays with the ExifTool adapter.
- ADR-0007 0.1.1 provisional → **accepted**; decision register updated; DOC-WBS 0.1.3 adds WP-010/013/014 design notes.
- Affected IDs: SPIKE-005, ADR-0007, DEP-008, DOC-WBS, DOC-STATUS, DOC-CHANGELOG, WP-010, WP-013, WP-014, NFR-FID-002. No new packages; no schema or migration impact.

## 0.1.9 — 2026-07-23

WP-004 filesystem and identity foundation (implemented):

- Context packet at `planning/context-packets/WP-004-context-packet.md`.
- `OrientExpress.Contracts`: DOC-COMPONENTS `Result<T>`/`ErrorInfo` common model; `NormalizedPath` (display + extended-length forms, NFR-COMP-002), `FileFingerprint` (standard/strong per DOC-DATAMODEL), `FileSnapshot`, `SnapshotComparison`, `FilesystemCapability` with NFR-COMP-001 classes.
- `OrientExpress.Application`: `IFileIdentityService`/`IAtomicFileOperations`/`IVolumeCapabilityProbe` ports plus pure `PathCanonicalizer` and `StagePathPlanner` (same-directory, unpredictable, exclusive-create).
- `OrientExpress.Infrastructure.Windows`: handle-based identity capture with drift comparison (`SIZE/LAST_WRITE/CONTENT_PREFIX/CONTENT/FILE_ID/VOLUME_CHANGED`, `MISSING`); reparse points and cloud placeholders policy-refused, never traversed or hydrated; SPIKE-006-qualified `ReplaceFileW(WRITE_THROUGH)` and create-path `MoveFileEx` (no overwrite flag — `DESTINATION_EXISTS` is typed) with actionable error classes; volume capability probe via DeleteOnClose handle; hardlink helper for alias handling.
- `upright doctor --probe-target <dir>`: live volume/identity/atomic-replace/create-path probes on an operator-named directory with guaranteed cleanup; the atomic probe demonstrates the SPIKE-006 file-id re-baseline. Doctor capability truth-telling updated (configuration and filesystem-identity now available).
- Tests: 15 real-NTFS integration tests (identity stability, hardlink aliases, prefix-digest drift with preserved size+timestamp, >280-char Unicode paths, locked/missing/reparse cases; symlink test skips without developer mode) plus 3 new CLI golden tests.
- Affected IDs: WP-004, PRD-FR-001 (identity portion), PRD-FR-010 (primitives portion), NFR-COMP-001, NFR-COMP-002, SPIKE-006 (consequences productionized), COMP-FILESYSTEM, COMP-NATIVE, DOC-STATUS, DOC-CHANGELOG. No new packages; no schema or migration impact.

## 0.1.8 — 2026-07-23

WP-003 configuration and schemas (implemented):

- Context packet at `planning/context-packets/WP-003-context-packet.md`.
- `OrientExpress.Contracts`: configuration record tree mirroring `schemas/config.schema.json` with strict unknown-member rejection (AC-037), plus `EffectiveConfig`/`ConfigIssue`.
- `OrientExpress.Application` (pure, package-free): built-in defaults mirroring schema defaults; deterministic five-layer merge with per-setting provenance (AC-036); validator mirroring schema bounds/enums/consts including `privacy.telemetry` pinned false; canonical effective-config digest (future plan sealing); `UPRIGHT_GROUP__KEY` environment mapping where unknown `UPRIGHT_` names and malformed values fail loudly.
- `OrientExpress.Cli`: `HostEnvironment` seam (hermetic tests, no process-global state); `ConfigLoader` fails closed — any parse, mapping, or validation issue means exit `21 INVALID_CONFIGURATION` and no work; global `--config`/`--profile`; working `config init` (starter file, refuses overwrite with `34 CONFLICT`/`CONFIG_EXISTS`, `--force`), `config show` (provenance + digest; `--redacted` digests path values; machine `config.effective` document), `config validate` (machine `config.issues` document on failure).
- Tests: 13 Application precedence/validation unit tests + 9 CLI config golden tests; gated-command list updated (config commands are now real).
- Affected IDs: WP-003, PRD-FR-019, NFR-MAINT-002, AC-036, AC-037, DOC-CONFIG (implemented as specified; no text change), COMP-CONFIG, DOC-STATUS, DOC-CHANGELOG. No new packages; no schema text changes; no migration impact.

## 0.1.7 — 2026-07-23

WP-002 CLI host and contracts (implemented) — first production feature code:

- Generated the DOC-DEVPLAN work-package context packet (`planning/context-packets/WP-002-context-packet.md`) before implementation.
- `OrientExpress.Contracts` gains the frozen exit-code registry (`CliExitCode` + symbols, cli-contract/1.0), the machine event record per `schemas/event.schema.json`, and capability-report records per `schemas/capability-report.schema.json` — all package-free per NFR-MAINT-001.
- `OrientExpress.Cli` replaces the stub with the production System.CommandLine host (DEP-002): full DOC-CLI command tree; global output options; UTF-8-without-BOM console encoding; host-expanded response files; Ctrl+C cancellation wired to exit `40 CANCELLED`; stable error lines (symbol + reason code + message + next action).
- Working commands: `version`, `doctor` (unwrapped schema-shaped capability report with real probes: long-paths registry value, temp writability, OS/framework/arch; capability truth-telling — only `cli-host` reports available), `completion powershell` (deterministic, model-derived).
- Every other documented command is registered but gated: refuses with `22 CAPABILITY_UNAVAILABLE`, reason `NOT_YET_IMPLEMENTED`, the gating WP ids, and — in machine formats — a consumable refusal document. Contract decision recorded in DOC-CLI 0.2.0 (registry frozen at WP-002 entry).
- `tests/OrientExpress.CliTests`: 44 golden tests (registry freeze, machine-stream purity, capability-report shape, gated refusals, grammar-error classes, response files, completion determinism, correlation ids, cancellation).
- Statuses: WP-002 → implemented; AC-033/AC-034 implemented; AC-003/AC-035/AC-048 partially implemented with named remainders.
- Affected IDs: WP-002, PRD-FR-002 (doctor portion), PRD-FR-017, PRD-FR-018, PRD-FR-027, NFR-UX-001, NFR-UX-002, AC-003, AC-033, AC-034, AC-035, AC-048, DEP-002, DOC-CLI, DOC-STATUS, DOC-CHANGELOG. No schema changes (existing draft schemas implemented as-is); no migration impact.

## 0.1.6 — 2026-07-23

SPIKE-001 corpus harness and truth protocol (spike in progress):

- Added the SPIKE-001 tooling under `spikes/SPIKE-001-corpus/` (`OrientExpress.Spike001` plus tests, registered in the solution): an opt-in, read-only inventory scanner emitting aggregates only — format signatures, JPEG EXIF orientation histogram (tag 0x0112, both TIFF endiannesses), SOF/IHDR dimensions, megapixel/size buckets, camera-make counts. Bounded 256 KiB probe, share-friendly handles, reparse points skipped, corrupt files counted not fatal. No paths, hashes, or thumbnails in output (privacy handling per the spike definition).
- 12 CI unit tests cover the parsers against synthetic byte fixtures; `spike001 self-test` verifies the end-to-end pipeline on a generated fixture tree (all 8 orientations, PNG, HEIC, corrupt, non-image) with committed evidence.
- Drafted the truth protocol: labeling guide (7-label set with first-class AMBIGUOUS/UNDECIDABLE, double labeling with Cohen's kappa, adjudication, consent rules, slice taxonomy) and dataset card template.
- SPIKE-001 remains in progress: the exit gate requires operator-owned photos (scanner runs, stratified consented tranche, double labeling). No new package dependencies.
- Affected IDs: SPIKE-001, DOC-STATUS, DOC-CHANGELOG. No schema or migration impact.

## 0.1.5 — 2026-07-23

SPIKE-008 job-store durability and scale (complete); ADR-0008 accepted:

- Added the SPIKE-008 harness under `spikes/SPIKE-008-job-store/` (`OrientExpress.Spike008` plus tests, registered in the solution): DOC-DATAMODEL schema prototype with trigger-enforced state-transition constraints (append-only events, sealed-plan immutability, forward-only journal checkpoints), WAL + NORMAL durability policy, incremental canonical plan sealing, guarded v1→v2 migration, kernel-kill crash matrix, and the 100k/1m synthetic-row gate.
- Gate met with wide margins: batched inserts 162k–185k rows/s (target 20k), 1m-row restart+counts 43 ms (target 2 s), migration 243 ms (target 30 s), integrity ok after both crash cases with exactly the committed rows surviving. Single-row transactions measured 6.5× slower than batching — the batched single-writer queue is a requirement.
- ADR-0008 provisional → **accepted** (0.1.1); decision register updated; DEP-011 marked selected with evidence.
- Dependency-intake finding: NuGet audit failed the build on the vulnerable transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (GHSA-2m69-gcr7-jv3q); pinned `SQLitePCLRaw.bundle_e_sqlite3` 2.1.12 as DEP-018 with a review rule on every DEP-011 bump.
- DOC-WBS 0.1.2 adds SPIKE-008 and SPIKE-009 design notes for WP-002/004/009/011/017.
- Affected IDs: SPIKE-008, ADR-0008, DEP-011, DEP-018, DOC-DATAMODEL (constraints exercised as specified; no text change), DOC-WBS, DOC-STATUS, DOC-CHANGELOG, WP-004, WP-009, WP-011, WP-017, NFR-SEC-002. No schema-contract or migration impact.

## 0.1.4 — 2026-07-23

SPIKE-009 CLI contract and automation ergonomics (complete):

- Added the SPIKE-009 harness under `spikes/SPIKE-009-cli-contract/` (`OrientExpress.Spike009` plus tests, registered in the solution): a representative DOC-CLI command slice on System.CommandLine 2.0.10 with machine event streams per `schemas/event.schema.json`, the exit-code registry, sealed digest-checked plans, response files, model-derived PowerShell completion, and simulated cancellation.
- 20 in-process golden contract tests run in CI; 11 out-of-process evidence scenarios measure byte-level behavior (no BOM, strict UTF-8, stream separation, `$LASTEXITCODE` values), all passing.
- Destructive guard proven: in-place plans refuse to apply without `--acknowledge-in-place` (`IN_PLACE_NOT_ACKNOWLEDGED`); tampered plans are refused with `30 STALE_PLAN`.
- DOC-CLI 0.1.1 records the binding contract decisions: exit aggregation order, empty machine stdout on grammar errors, host-side rejection of option-like positional tokens (parser binds them silently), explicit UTF-8-without-BOM console encoding on Windows, `\uXXXX` escaping of non-ASCII, and host-expanded response files.
- Pinned System.CommandLine 2.0.10 centrally (DEP-002 qualification evidence recorded). WP-002 and WP-018 are unblocked on their spike gate; registry numerics freeze at WP-002 entry.
- Affected IDs: SPIKE-009, DEP-002, ADR-0003 (plan-consumption contract exercised), ADR-0010, DOC-CLI, DOC-STATUS, DOC-CHANGELOG, WP-002, WP-018. No schema changes (event schema exercised as-is); no migration impact.

## 0.1.3 — 2026-07-23

SPIKE-006 closure under single-operator governance:

- Marked SPIKE-006 `complete`: its exit gate (recovery state machine proven on NTFS, unsupported conditions capability-gated, zero source-loss fault matrix) is met by the recorded evidence.
- ADR-0009 0.1.1 records the qualification evidence: `ReplaceFileW(REPLACEFILE_WRITE_THROUGH)` is the sole in-place primitive; `MoveFileEx` replace is disqualified for in-place use.
- DOC-FSSAFE 0.1.1 records measured primitive behavior and the normative caveat that file IDs are pre-commit-only identity (re-baseline after commit).
- DOC-WBS 0.1.1 adds spike-driven design notes for WP-004, WP-011, WP-012, and WP-015.
- STATUS 0.1.3 records the single-operator governance mode: documentation gates (validator, CI, tests, evidence rules) are the control surface; review sign-offs are the operator's own acceptance.
- Affected IDs: SPIKE-006, ADR-0009, DOC-FSSAFE, DOC-WBS, DOC-STATUS, DOC-CHANGELOG, WP-004, WP-011, WP-012, WP-015. No schema or migration impact; no dependency changes.

## 0.1.2 — 2026-07-23

SPIKE-006 NTFS pilot evidence (crash-safe atomic replace):

- Added the SPIKE-006 decision harness under `spikes/SPIKE-006-atomic-replace/` (`OrientExpress.Spike006` plus its test project, registered in the solution): the DOC-FSSAFE in-place protocol with a durable JSON-lines journal, kernel-level crash injection at every step boundary, the recovery state machine, and primitive qualification of `ReplaceFileW`/`MoveFileEx`.
- Encoded the recovery state machine test-first: 31 unit tests, including matrix rows, torn-journal tolerance, and totality over the observation space, run in CI with the solution.
- Recorded machine-readable evidence (24 scenarios): all twelve fault-injection cases recover per the DOC-FSSAFE matrix with zero source-loss cases and idempotent re-recovery; `ReplaceFileW` preserves creation time, attributes, alternate data streams, and DACL but **not** the NTFS file ID; `MoveFileEx` replace loses creation time, ADS, and identity; sharing/read-only/missing-target error classes measured; cross-volume rejection stays capability-gated (single-volume machine).
- Recorded the WP-004 design consequence: handle-based identity must re-baseline after commit because the file ID does not survive `ReplaceFileW`.
- Updated the spike register (SPIKE-006 → evidence-recorded-pending-review) and STATUS; in-place mode remains blocked pending review sign-off, non-NTFS capability classes, and undo-path evidence.
- Affected IDs: SPIKE-006, RISK-005, ADR-0009 (evidence pending review), WP-004, WP-011, WP-012, WP-015, DOC-FSSAFE (reviewed follow-up), DOC-STATUS, DOC-CHANGELOG. No schema or migration impact; no new package dependencies (NFR-SEC-002 unchanged).

## 0.1.1 — 2026-07-22

WP-001 repository and engineering baseline:

- Implemented the `OrientExpress` solution layout: nine source projects plus unit and architecture test projects per DOC-ARCH. Project names use the namespace-safe `OrientExpress.*` form (C# namespaces cannot contain hyphens); the CLI builds as the provisional executable `upright`.
- Enforced NFR-MAINT-001 layer rules with architecture conformance tests (harness for TEST-NFR-025).
- Established deterministic builds toward NFR-SEC-002: `global.json` SDK pin, central package management, NuGet lock files with locked-mode CI restore, single mapped package source, warnings-as-errors, analyzers, and an `.editorconfig` style gate.
- Added GitHub Actions CI: documentation/catalog/traceability validation, dependency-graph regeneration check, and Windows build, format, and test jobs.
- Adopted xunit.v3 3.2.2 per DEP-012 and inventoried test tooling as DEP-015 through DEP-017 with sources SRC-029 and SRC-030.
- Repaired a broken export link in `research/spec-02.md` left over from the PhotoUpright rename; added `.gitignore`/`.gitattributes` and stopped tracking Python bytecode.
- Removed the stale one-shot package artifacts `SHA256SUMS.txt` and `VALIDATION-REPORT.txt`: validation now runs in CI on every change, and release checksums are produced at packaging time per DOC-CICD.
- Affected IDs: WP-001, NFR-MAINT-001, NFR-SEC-002, DEP-012, DOC-ARCH, DOC-STATUS, DOC-CHANGELOG. No schema or migration impact.

## 0.1.0 — 2026-07-21

Initial professional planning baseline:

- Established product charter, scope, workflows, 28 functional requirements, 28 non-functional requirements, and 50 acceptance criteria.
- Defined Windows-first modular-monolith architecture and 24 components.
- Defined EXIF-plus-inferred-transform semantics and a calibrated four-rotation orientation pipeline.
- Established immutable scan/apply jobs, safe-copy defaults, guarded in-place recovery, JPEG fidelity policy, metadata policy, and output verification.
- Created ten technical spikes, 24 work packages, eight evidence-gated milestones, 20 risks, and 12 ADRs.
- Added five external JSON Schemas, machine-readable catalogs, dependency graphs, and validation tooling.

Future entries must identify affected requirement, ADR, schema, and migration IDs.
