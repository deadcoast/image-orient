---
document_id: DOC-ML
title: ML Orientation Engine
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: ML
depends_on: [DOC-ARCH, DOC-RESEARCH, ADR-0004, ADR-0005]
traces_to: [PRD-FR-004, PRD-FR-005, PRD-FR-006, PRD-FR-020, PRD-FR-024, PRD-FR-025, NFR-ML-001, NFR-ML-002, NFR-ML-003, NFR-ML-004]
---

# ML orientation engine

## Objective

Estimate the additional quarter-turn that makes the displayed image upright, while minimizing false automatic rotations. The engine returns an explainable assessment, not merely a class index.

## Input normalization

1. Probe file and metadata without modifying it.
2. Decode a bounded analysis image, respecting color profile policy and decompression limits.
3. Apply EXIF Orientation 1–8 to create the intended metadata display image `I`.
4. Scale/pad according to the model manifest without distorting aspect ratio.
5. Construct candidates `I0`, `I90`, `I180`, `I270`.
6. Record every mapping needed to project model coordinates back to displayed/source coordinates.

The analysis long edge and minimum face size are configuration/calibration inputs, not arbitrary per-user tuning in the stable profile.

## Baseline model

A verified YuNet-class ONNX face detector is the baseline candidate because it is compact and returns:

- face bounding box;
- left/right eye;
- nose tip;
- left/right mouth corner;
- detector score.

The exact model artifact, input shape, preprocessing, postprocessing, NMS, score threshold, license, digest, and output tensor contract live in the model manifest. A model name alone is insufficient provenance.

## Four-candidate execution

Each candidate is evaluated with identical model/runtime settings. The implementation may batch candidates only when the selected runtime/model proves equivalent. Caching is keyed by source digest, displayed-thumbnail descriptor, model digest, runtime compatibility class, and preprocessing version.

## Per-face geometric features

Let eye points be `eL,eR`, mouth corners `mL,mR`, nose `n`, and box `b`.

Candidate features include:

- detector score and logit;
- normalized face area and centrality;
- landmark containment and distance from clipped edges;
- eye-line angle `atan2(eR.y-eL.y, eR.x-eL.x)` after left/right semantic handling;
- mouth-line angle;
- eye midpoint above nose and nose above mouth midpoint;
- normalized eye–nose and nose–mouth vertical separations;
- inter-eye distance/face-width ratio;
- landmark triangle orientation and degeneracy;
- face aspect and visible-area indicators;
- disagreement between detector semantic left/right and image geometry;
- candidate-to-candidate detection persistence;
- count and distribution of faces.

Hard rules reject invalid/nonfinite landmarks and implausible geometry rather than allowing extreme values to dominate.

## Multi-face aggregation

For each candidate:

- weight faces by bounded function of detector confidence and visible area;
- cap dominance of a single giant face;
- measure agreement of individual upright evidence;
- downweight tiny/edge-clipped faces;
- flag contradictory strong faces;
- preserve top-N face summaries, not identity.

A group selfie with several agreeing faces strengthens evidence. Strong disagreement forces REVIEW unless a later proven policy handles it.

## Candidate score

Initial spike form:

```text
raw(candidate) =
  w1 * detector_quality
+ w2 * landmark_vertical_order
+ w3 * eye_horizontalness
+ w4 * mouth_horizontalness
+ w5 * facial_proportion_plausibility
+ w6 * visibility_and_containment
+ w7 * multi_face_agreement
- penalties(clipping, degeneracy, contradiction, no_face)
```

Weights are not hand-tuned into production. SPIKE-002 generates a labeled feature dataset, then compares:

- rule-based score;
- regularized multinomial/logistic model;
- gradient-boosted/calibration-friendly small model;
- optional compact learned orientation head.

The simplest model that meets gates wins.

## Metadata prior

EXIF orientation is already applied before inference. Metadata provenance/consistency may become a weak prior—for example, trusted camera files versus edited screenshots—but cannot override strong contradictory visual evidence or hard safety rules. The prior and its source are recorded.

## Optional evidence

### Scene fallback

A compact four-way scene orientation model may run only for no-face/weak-face cases and only after an ablation shows improved high-precision coverage. It must have its own model manifest, calibration, slices, and out-of-distribution behavior.

### Sequence consensus

Files may be grouped by bounded capture-time/device/path heuristics. Neighbor decisions can act as a tie-breaker only when individual evidence is near a threshold and no hard contradiction exists. Sequence data never creates face identity and never overrides safety gates.

## Calibration

Calibration is fitted on a development set and evaluated on untouched holdout data. Candidate methods include temperature scaling, vector scaling, isotonic calibration, or conformal/risk-control methods suitable to sample size. Persist:

- calibration artifact digest/version;
- training/evaluation dataset cards;
- feature/model schema;
- class probabilities;
- top score and top1–top2 margin;
- estimated risk/confidence interval;
- slice warnings.

## Decision policy

Automation requires all applicable gates:

1. eligible/supported file and transform;
2. minimum valid evidence;
3. top class score above threshold;
4. top-two margin above threshold;
5. no hard contradictory multi-face or OOD rule;
6. class/slice-specific calibrated risk below ceiling;
7. output safety/fidelity plan available.

Otherwise decision is REVIEW/UNSUPPORTED/ERROR.

Proposed reason codes include:

- `UPRIGHT_HIGH_CONFIDENCE`
- `ROTATION_HIGH_CONFIDENCE`
- `METADATA_NORMALIZATION_ONLY`
- `NO_FACE_EVIDENCE`
- `FACE_TOO_SMALL`
- `LANDMARKS_INVALID`
- `CANDIDATES_AMBIGUOUS`
- `MULTI_FACE_DISAGREEMENT`
- `LOW_CONFIDENCE`
- `LOW_MARGIN`
- `OUT_OF_DISTRIBUTION`
- `SCENE_FALLBACK_DISABLED`
- `UNSUPPORTED_TRANSFORM_FIDELITY`

## Precision-first thresholds

Release targets:

- accepted selfie precision ≥99.5%;
- accepted already-upright false rotation ≤0.1%;
- accepted selfie coverage should reach ≥85% only after those pass.

Thresholds may differ by evidence stratum (single clear face, multiple agreeing faces, no-face fallback) but cannot be user-adjustable without entering an expert/unsafe profile whose effect is explicit.

## Evaluation slices

At minimum:

- correct class and EXIF Orientation value;
- camera/app/device family;
- edited versus camera-original;
- face count and face-size bins;
- frontal/profile/extreme pose;
- sunglasses/masks/hats/occlusion;
- low light/blur/backlight;
- skin tone/age/gender presentation where responsibly labeled and consented;
- lying-down/intentional sideways/artistic cases;
- screenshots, illustrations, mirrors, text-heavy images;
- already-upright negatives;
- no-face and false face detections;
- file format and decode path;
- runtime/provider.

## Reproducibility

Every assessment records source digest, displayed-thumbnail recipe, model/calibration/policy digests, runtime/provider version, preprocessing/postprocessing version, numeric mode, and feature schema. Raw photos are not embedded in diagnostic events.

## Rollout protection

A new model/policy runs shadow evaluation against the prior accepted release. Activation requires:

- schema compatibility;
- complete corpus regression;
- no unacceptable slice regression;
- provider equivalence;
- signed/digest-pinned bundle;
- rollback test;
- updated model/policy card and ADR if behavior materially changes.
