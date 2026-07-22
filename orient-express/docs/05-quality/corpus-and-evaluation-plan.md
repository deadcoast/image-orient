---
document_id: DOC-EVAL
title: Corpus, Ground Truth, Calibration, and ML Evaluation Plan
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: ML Quality
depends_on: [DOC-TEST, DOC-ML, DOC-SEC]
traces_to: [SPIKE-001, SPIKE-002, NFR-ML-001, NFR-ML-002, NFR-ML-003, NFR-ML-004]
---

# Corpus and evaluation plan

## Purpose

The release claim is about **accepted automatic orientation decisions on the target distribution**, not face-detection benchmark AP. The corpus and truth process therefore govern product viability.

## Data sources

1. **Opt-in target-library sample:** inventory plus selected images from the intended user domain.
2. **Consented internal capture:** controlled devices, rotations, lighting, poses, occlusion, mirrors, multiple faces.
3. **Licensed public datasets:** only where licenses allow the intended evaluation and orientation labels can be constructed responsibly.
4. **Synthetic transforms:** rotate/mirror known upright images to test transform algebra, while preventing leakage and shortcut dependence.
5. **Adversarial/non-target set:** screenshots, drawings, text, scenery, false-face patterns, corrupted files, artistic orientation.

Synthetic rotations cannot be the sole evaluation source because they make ground truth easy while missing camera/edit/metadata artifacts and intentional sideways composition.

## Privacy and consent

- Inventory mode collects counts/hashes/features only according to explicit consent.
- Copying personal images to the governed corpus requires explicit selection and documented retention/access.
- No identity labels or cross-image person IDs.
- Demographic slices, if collected, use voluntary/ethical protocols and only to detect harmful quality disparities.
- Review thumbnails and raw images are access-controlled, encrypted at rest where applicable, and excluded from ordinary CI artifacts.
- Dataset withdrawal has a documented process and triggers derived-artifact review.

## Dataset card

Each corpus release records:

- purpose and exclusions;
- provenance/license/consent;
- collection dates and devices/apps;
- size and duplicate policy;
- orientation and EXIF distributions;
- face-count/size/pose/quality distributions;
- demographic/lighting/occlusion slices when available;
- labeling protocol/agreement;
- train/calibration/dev/holdout split logic;
- known limitations and retention.

## Ground-truth label

Primary label is one of:

- `UPRIGHT_0`
- `ROTATE_90_CW`
- `ROTATE_180`
- `ROTATE_270_CW`
- `INTENTIONAL_NON_UPRIGHT_OR_AMBIGUOUS`
- `UNLABELABLE_OR_CORRUPT`

Annotators view the image as typical software displays it using metadata, then choose the additional rotation needed for intended upright viewing. They separately label whether sideways/lying orientation appears intentional. Stored-pixel and EXIF facts are machine-derived, not guessed.

## Labeling protocol

- Two independent labels for calibration/holdout.
- Blind to model output and filenames/device identifiers where practical.
- Adjudication by a third trained reviewer for disagreement.
- Measure raw agreement and Cohen/Fleiss kappa where applicable.
- Ambiguous/intentional cases remain a dedicated abstention slice; do not force consensus.
- Record confidence and reason taxonomy.

## Splitting

Prevent leakage by grouping near-duplicates, burst sequences, same source edit chain, and, where known without identity profiling, collection session/device/path groups. The final holdout is frozen and never used to tune thresholds. A second temporal/external pilot is preferred before v1.

## Metrics

### Primary

- accepted precision overall and by action;
- false rotation rate on already-upright accepted images;
- accepted coverage;
- REVIEW rate;
- critical slice precision;
- 95% confidence intervals.

### Calibration

- reliability diagrams;
- expected/max calibration error;
- Brier/log loss;
- risk-coverage curve;
- top-two margin distribution;
- selective classification/AURC.

### Diagnostic

- confusion matrix;
- face detection/landmark validity;
- no-face and multi-face rates;
- reason-code distribution;
- latency by slice/provider;
- disagreement with prior release.

## Statistical release rule

Point estimates alone are insufficient. Define minimum sample counts and one-sided confidence bounds for the false-rotation ceiling. Rare-error claims may require tens of thousands of accepted negatives or sequential/risk-limiting evaluation. SPIKE-001 must calculate feasible sample sizes and propose the exact interval method before freezing “99.5%/0.1%” as marketed guarantees.

## Slice gates

A critical slice cannot be hidden by overall averages. For small slices, the policy may abstain more aggressively or mark the slice unsupported rather than asserting weak evidence. Any accepted regression requires product/quality approval and documented impact.

## Calibration workflow

1. Lock feature/model schema.
2. Train candidate score model on training split.
3. Fit calibration/thresholds on calibration split.
4. Select policy by precision-first risk/coverage objective.
5. Evaluate once on frozen holdout.
6. Run provider equivalence and compatibility sets.
7. Produce signed model/policy card.
8. Shadow on pilot library before activation.

## Model/policy card

Records intended use, prohibited use, corpus, metrics/intervals, slices, thresholds, abstention behavior, known failures, model/runtime/calibration digests, license, security/resource limits, and rollback.

## Regression policy

Every model/runtime/provider/preprocessing/feature/calibration/policy change runs the full frozen suite. The candidate is compared against the current release on identical inputs. A coverage gain cannot compensate for violating precision or upright false-rotation gates.
