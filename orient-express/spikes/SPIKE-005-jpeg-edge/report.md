# SPIKE-005 report — JPEG lossless rotation edge policy

**Status:** complete — accepted under single-operator governance (2026-07-23)
**Run:** 2026-07-23, Windows 11 (build 10.0.26200), .NET 10.0.8; libjpeg-turbo 3.2.0 (DEP-008, winget `libjpeg-turbo.libjpeg-turbo.VC`, tool digests in results)
**Raw evidence:** [`results/spike005-results.json`](results/spike005-results.json) · **Harness:** [`README.md`](README.md)

## Question and exit gate

What exactly happens at non-iMCU-aligned edges under jpegtran quarter-turns,
and which default policy guarantees "no silent crop or re-encode"
(ADR-0007)? Gate: default policy and CLI options approved; no silent
crop/re-encode; test vectors committed (the deterministic fixture generator
is the committed vector source).

## Results — 108-case matrix, all safety expectations pass

- **`-perfect` is the guarantee mechanism.** It refused 26 of 36
  combinations (all where the transform cannot be edge-perfect: r180 refuses
  all 10 non-aligned fixtures; r90/r270 refuse 8 each) and succeeded on 10.
  Every success was proven **coefficient-lossless by round-trip**: rotate,
  rotate back, decode → byte-identical to the original decode.
- **Default mode never crops and never destroys data in 3.2.0.** All 36
  default-mode rotations produced full-dimension outputs, and all 36 were
  round-trip lossless — non-aligned edge blocks are carried reversibly, but
  their edge placement is imperfect (visible seam risk). This maps cleanly to
  the config enum `jpegEdge: preserve-partial`.
- **`-trim` visibly changes dimensions** (23 of 36 cases resized to
  MCU-derived bounds) — deliberate, visible data loss; maps to
  `jpegEdge: trim`. Never silent.
- **Markers:** jpegtran `-copy all` preserved the COM fixture marker;
  `-copy none` dropped it. WP-010 must always run with explicit `-copy all`
  semantics and re-apply metadata through the metadata adapter afterwards —
  marker survival is never left to tool defaults.

## Key verification finding (binding on WP-013)

**Decoded-pixel comparison is the wrong losslessness test.** DCT-domain
rotation is coefficient-exact, yet decoded pixels differ from a spatially
rotated decode by up to ±3 per channel (4.3% of bytes at 4:4:4 r90; ~7% at
4:2:0) because the integer IDCT is not transpose-commutative and chroma
upsampling is not rotation-equivariant. 180° at 4:4:4 differs by only 8
bytes. Consequence: the WP-013 verifier must verify losslessness by
**inverse-transform round-trip or coefficient digest — never by comparing
decoded pixels against a spatially rotated reference**. Decoded-pixel deltas
≤4 are the expected, correct behavior of a lossless transform.

## Recommended policy (matches `config.schema.json` `output.jpegEdge`)

| Config value | Mechanism | Behavior |
|---|---|---|
| `abort` (default) | `jpegtran -perfect`; nonzero exit → refuse with `JPEG_EDGE_NOT_PERFECT` | Guarantee-grade: only coefficient-lossless, edge-perfect transforms ship |
| `preserve-partial` | default mode | Reversible, full-dimension, imperfect edge placement disclosed per file |
| `trim` | `-trim` | MCU-aligned crop, dimension change disclosed per file |
| `reencode` | raster path (WP-010) | Not a jpegtran mode; explicit quality loss |

All jpegtran invocations use `-copy all`; metadata authority remains the
ExifTool adapter (SPIKE-004).

## Limitations

- Baseline (sequential) JPEGs only; progressive inputs and arithmetic coding
  not matrixed (WP-010 adds them to the golden corpus).
- Viewer-compatibility testing (Photos app, browsers) deferred to WP-014
  beta evidence; outputs are standard baseline JPEGs from jpegtran.
- Behavior is pinned to libjpeg-turbo 3.2.0 (digests recorded); the default
  mode's reversible-edge behavior must be re-verified on any DEP-008 bump.

## Affected items

- **ADR-0007 0.1.1** — provisional → accepted; proof gate met.
- **DEP-008** — qualified at 3.2.0; register updated with install source and
  re-verification rule.
- **DOC-WBS** — design notes for WP-010/WP-013/WP-014.
- **planning/spike-register.csv** — SPIKE-005 → complete.

## Spike completion checklist (DOC-SPIKES)

- [x] Falsifiable question and thresholds declared before the run.
- [x] Inputs/fixtures documented — deterministic synthetic gradients.
- [x] Harness repeatable from a clean checkout (`README.md`; tools dir parameterized).
- [x] Raw output machine-readable with tool digests.
- [x] Failure cases included (26 perfect-refusals characterized by rotation and alignment).
- [x] Recommendation compares alternatives (default vs perfect vs trim vs re-encode).
- [x] ADR and dependency register updated — ADR-0007 0.1.1; DEP-008.
- [x] Follow-on work package plans updated — DOC-WBS notes for WP-010/013/014.
- [x] Artifact retained in-repo under the evidence policy.
