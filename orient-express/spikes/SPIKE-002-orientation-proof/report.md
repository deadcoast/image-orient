# SPIKE-002 report — four-rotation YuNet orientation proof (pilot)

**Status:** in progress — harness complete, mechanics pilot passing; the ≥99% precision gate requires the SPIKE-001 corpus tranche
**Run:** 2026-07-23; YuNet 2023mar (digest-pinned, MIT) on ONNX Runtime 1.27.1 CPU
**Raw evidence:** [`results/spike002-003-results.json`](results/spike002-003-results.json) · **Harness:** [`README.md`](README.md)

## Question and exit gate

Can face detections across the four rotation candidates reliably identify
the upright orientation? Gate: reproducible evaluation report; ≥99%
precision on pilot accepted cases, or a documented case for a secondary
model. **This run is the mechanics pilot on one public-domain portrait; the
statistical gate stays open until the corpus exists.**

## Pilot results — 4/4 rotations recovered

The four-candidate approach (present the image, score all four
counter-rotations, choose the maximum):

| Presented | Candidate scores (0/90/180/270 restore) | Chosen | Correct |
|---|---|---|---|
| 0° | **0.947** / 0 / 0.841 / 0.543 | 0° | ✔ |
| 90° | 0 / 0.841 / 0.543 / **0.947** | 270° | ✔ |
| 180° | 0.841 / 0.543 / **0.947** / 0 | 180° | ✔ |
| 270° | 0.543 / **0.947** / 0 / 0.841 | 90° | ✔ |

The eyes-above-mouth landmark check agreed with the chosen candidate in all
four cases. Detection is bitwise-deterministic, so these numbers reproduce
exactly.

## The finding that shapes ADR-0005

**An upside-down face still scores 0.841** (vs 0.947 upright) and a 90°
face 0.543 — YuNet fires on inverted faces with high confidence. The
winning margin on this easy portrait is only **0.106**. Consequences:

1. Raw score thresholds cannot decide orientation; the decision variable
   must be the **cross-candidate margin**, exactly as ADR-0005's calibrated
   abstention design assumes.
2. The 0°/180° pair is the confusable axis; the landmark geometry
   (eyes-above-mouth) is a genuinely independent disambiguator and agreed
   4/4 here — it belongs in the WP-007 feature vector.
3. Calibration on the labeled corpus is not optional polish; it is the
   mechanism that turns these margins into a ≥99%-precision decision rule
   with honest abstention.

## Limitations

- One image, one face, studio lighting. No pose/occlusion/multi-face/no-face
  slices; no precision statistics of any kind are claimed.
- Stretch resize to 640×640 (no letterboxing yet); aspect-ratio distortion
  tolerance is untested.
- Mirrored EXIF cases (2/4/5/7) not yet exercised.

## Remaining for the exit gate

1. SPIKE-001 corpus tranche (operator photos, consented, double-labeled).
2. Run this harness across the tranche; report precision/coverage per slice
   with margins and abstention curves.
3. Decide ≥99% precision reachability or document the secondary-model case.

## Affected items

- **ADR-0005 0.1.1** — pilot evidence note added; stays provisional pending
  the corpus precision evaluation.
- **DEP-006** — YuNet digest-pinned with MIT license recorded.
- **planning/spike-register.csv** — SPIKE-002 → in-progress (pilot passing).
