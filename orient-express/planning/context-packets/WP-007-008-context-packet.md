# WP-007/WP-008 context packet — orientation engine and provisional decision policy

Generated 2026-07-24 per DOC-DEVPLAN ("Work-package context packet").
Covers WP-007 (orientation feature engine) and WP-008 (decision policy),
delivered together as the SPIKE-002 stack productionized.

## Recorded deviation: abstention-until-calibrated

WP-008's calibrated thresholds require the SPIKE-001/002 corpus evidence,
which remains operator-gated. Under single-operator governance the policy
ships PROVISIONAL and UNCALIBRATED: it rotates only when every signal
agrees (confident-face vote winner + upright landmark geometry + relative
margin >= 0.08) and parks everything else for Review. Every assessment
records `calibrated: false` and the policy identity
`four-candidate-vote-geometry-margin/provisional-uncalibrated`; the model
and policy identities are written into the job manifest. Calibration
replaces the constants in ONE place (`FourCandidatePolicy`) when WP-020's
corpus evidence exists. Detection is OPT-IN (`scan --detect`), never
default.

## Delivered

- `Contracts.Orientation`: `PixelRaster`, `FaceObservation` (five YuNet
  landmarks; eyes-above-mouth geometry), `CandidateSummary`,
  `OrientationDecision` (Upright/Rotate90Cw/180/270Cw/Review),
  `OrientationAssessment` (all four candidates, margins, reason codes,
  model/runtime/policy provenance, calibrated flag).
- `Application.Orientation`: `IPixelDecoder`/`IFaceDetector` ports;
  `PixelRasterOperations` (exact quarter-turn rotation, bilinear resize —
  SPIKE-002 math); `FourCandidatePolicy` (pure, exhaustively unit-tested
  decision matrix); `FourCandidateOrientationEngine` (WP-007: one decode
  per photo, four candidate presentations, evidence summaries).
- `Infrastructure.Imaging.DjpegPixelDecoder`: bounded analysis decode via
  qualified djpeg (DEP-008), downscaled to the 960 analysis edge;
  read-only, never the transform path.
- `Infrastructure.Inference.YuNetFaceDetector`: the SPIKE-002/003 YuNet
  decode (640x640 BGR, twelve heads, FaceDetectorYN post-processing, NMS
  0.3) behind the WP-006 `IInferenceRuntime` port — the model artifact is
  digest-verified at load, provenance comes from the registry.
- `scan --detect`: clean-tag JPEGs that the deterministic path would Keep
  are assessed; confident decisions become Normalize rows
  (`ML_DECIDED`), confident-upright stays Keep (`ML_CONFIRMED_UPRIGHT`),
  everything else becomes Review rows with named reasons
  (`ML_NO_FACE`, `ML_NO_CONFIDENT_FACE`, `ML_GEOMETRY_DISAGREES`,
  `ML_MARGIN_TOO_SMALL`). Refuses upfront without an active model (22) or
  djpeg (22). The scan summary states the uncalibrated scope in plain
  words.

## Evidence

- 6 pure policy decision-matrix tests.
- End-to-end CLI test on the digest-pinned pilot portrait: rotated 90
  clockwise with a CLEAN tag (the case the deterministic path cannot
  fix) → `scan --detect` plans the rotation via the production
  registry/runtime → `apply` produces a verified upright output. Skips
  when the pinned model/portrait cache or tools are absent.
- Operator's first corpus measurement (21 photos, rule A): 80.95% with
  confidently-wrong cases — the reason the provisional policy is
  conservative and abstains.

## Honest scope

- UNCALIBRATED: accuracy/coverage on real corpora is measured, not
  promised; the SPIKE-002 evaluate harness remains the measurement tool
  and the >=99 percent precision gate still needs the operator's corpus
  (WP-020).
- Review rows are recorded and reported but have no workflow yet
  (WP-016); apply ignores them.
- Multi-face contradictory-pose weighting, metadata priors, and the
  calibration artifact itself are WP-020 scope.
