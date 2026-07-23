# SPIKE-001 labeling guide and truth protocol (draft 0.1)

Governs ground-truth creation for the orientation corpus. This protocol must
be followed before any accuracy number is quoted anywhere in the project
(NFR-ML gates). It operationalizes DOC-CORPUS.

## What "upright" means

The label is the rotation that makes the photograph **semantically upright
for a human viewer**: horizons horizontal, people/buildings head-up, text
readable where text dominates. The label describes the rotation needed for
the **rendered pixels the labeler sees** (viewer already applied EXIF
display orientation — record both the viewer used and the raw EXIF value).

## Label set

| Label | Meaning |
|---|---|
| `UPRIGHT` | No rotation needed. |
| `NEEDS_90_CW` | Rotating 90° clockwise makes it upright. |
| `NEEDS_180` | Rotating 180° makes it upright. |
| `NEEDS_270_CW` | Rotating 270° clockwise (90° CCW) makes it upright. |
| `AMBIGUOUS` | A reasonable person could accept ≥2 rotations (top-down shots, abstract textures, symmetric scenes). |
| `UNDECIDABLE` | Content gives no orientation cue at all. |
| `EXCLUDED` | Not a photograph (screenshot, scan, render) or fails consent review. |

`AMBIGUOUS`/`UNDECIDABLE` are first-class outcomes, not failures — they feed
the abstention calibration (ADR-0005). Labelers never guess.

## Double labeling and adjudication

1. Every image is labeled independently by two labelers who cannot see each
   other's labels or the EXIF value.
2. Agreement is measured per slice with Cohen's kappa; the exit gate requires
   the measured kappa to be reported alongside any accuracy claim.
3. Disagreements go to a third adjudicator; the adjudicated label is final
   and the disagreement is retained as a difficulty signal.
4. Labels are append-only records: relabeling supersedes, never overwrites.

## Privacy and consent

- Photos never leave the owner's machine without written consent recorded in
  the dataset card. The inventory scanner emits **aggregates only**.
- No faces are cropped, embedded, or identified; the pipeline uses face
  geometry transiently and discards it (DOC-SECURITY).
- Corpus images copied into a labeled tranche require explicit per-source
  consent; each tranche lists its consent reference.

## Slice taxonomy (v0)

Record for every labeled image: capture class (phone / dedicated camera /
unknown), people present (yes/no), scene (indoor/outdoor/document/other),
EXIF orientation value (1–8/absent), format, and megapixel bucket from the
scanner. Accuracy is always reported per slice as well as overall; a slice
with fewer than 100 samples is reported as insufficient, not extrapolated.

## Tranche flow

1. Run `spike001 scan` on an opted-in tree; review aggregate skew.
2. Sample a tranche stratified by the aggregate distribution.
3. Copy only consented files into the tranche folder; fill the
   [dataset card](dataset-card-template.md).
4. Double-label, adjudicate, record kappa.
5. Freeze the tranche: manifest with per-file SHA-256, labels CSV, card.
