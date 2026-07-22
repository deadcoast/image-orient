---
document_id: DOC-RESEARCH
title: Research Summary
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Research
depends_on: []
traces_to: [SPIKE-001, SPIKE-002, SPIKE-003, SPIKE-004, SPIKE-005, SPIKE-006, SPIKE-007, SPIKE-008, SPIKE-009, SPIKE-010]
---

# Research summary

## Finding 1 — Existing autorotation is mostly metadata-driven

ImageMagick `-auto-orient`, jhead, and similar utilities can apply EXIF orientation. They are useful fast paths and prior art, but they do not solve visually wrong files whose orientation metadata is absent, stripped, normalized incorrectly, or itself wrong. This project therefore needs a metadata path **and** a visual inference path.

## Finding 2 — Selfies enable interpretable local evidence

A compact face detector such as YuNet returns a face box and five landmarks. Running it on four quarter-turn candidates allows the system to compare detector confidence, eye line, eye/nose/mouth ordering, clipping, face size, and agreement across faces. This is more interpretable than using a single opaque “portrait/landscape” output. It still requires domain calibration and abstention; face detector benchmark scores do not establish orientation decision quality.

## Finding 3 — ONNX is the best integration boundary

Windows ML and ONNX Runtime provide local inference options for C#. Windows ML offers Windows-managed provider integration, while a direct ONNX Runtime CPU adapter gives a controlled fallback. The architecture should own `IInferenceRuntime` and keep model preprocessing/postprocessing outside provider-specific code.

## Finding 4 — Metadata is its own subsystem

Metadata fidelity cannot be delegated to a generic image save operation. ExifTool offers unusually broad read/write semantics and a persistent `-stay_open` integration, but it must be isolated, versioned, timed out, license-reviewed, and validated against an actual device/format corpus.

## Finding 5 — JPEG rotation has non-obvious edge semantics

Quarter turns can often be lossless in the DCT domain. Some JPEG dimensions contain partial MCU edge blocks, creating explicit choices: refuse, trim, preserve partial behavior, or decode/re-encode. A professional tool must expose and record that policy, not silently crop or recompress.

## Finding 6 — Windows commit safety requires more than `File.Move`

Correct in-place replacement needs file identity checks, same-volume staging, durable write/journal boundaries, independent output verification, atomic replacement where supported, backup verification, restart reconciliation, and behavior for locks, antivirus, cloud placeholders, links, alternate streams, and unsupported filesystems.

## Finding 7 — “Broad format support” is capability-specific

WIC and ImageMagick/Magick.NET can broaden decode/encode support, but installed codecs, delegate libraries, metadata behavior, package size, security surface, and licensing differ. Support must be reported per operation: read, metadata-read, transform, metadata-write, verify, and lossless path.

## Finding 8 — Professional quality depends on the corpus

The hardest product claim is not that a model runs; it is that accepted automatic decisions are safe on the user's real distribution. The project needs opt-in inventory, ground-truth rules for intentional sideways images, double labeling, demographic/device/lighting/pose slices, held-out evaluation, confidence intervals, model/policy cards, and regression gates.

## Consequence

The correct next step is not broad feature coding. It is to execute the ten technical spikes, update provisional ADRs with evidence, and unlock implementation packages in dependency order.
