---
document_id: DOC-GLOSSARY
title: Glossary
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: []
traces_to: []
---

# Glossary

| Term | Definition |
|---|---|
| Upright orientation | The display transform under which people and scene gravity cues appear intentionally upright. It is not synonymous with portrait aspect ratio. |
| Stored pixels | Pixel matrix as encoded in the file before applying orientation metadata. |
| Display transform | EXIF Orientation 1–8 transform a conforming viewer applies, including mirrors and quarter turns. |
| Additional rotation | One of 0°, 90°, 180°, or 270° inferred after normalizing metadata semantics for analysis. |
| Physical normalization | Rewriting the pixel representation so its intended display is upright and setting orientation metadata to 1. |
| Candidate | One of the four additional-rotation hypotheses evaluated by the orientation engine. |
| Evidence | Detector confidence, landmarks, geometry, face visibility, agreement, metadata prior, and optional bounded context. |
| Calibration | Mapping raw evidence/scores to probabilities or uncertainty estimates validated on held-out data. |
| Abstention | Intentionally returning REVIEW rather than automating an uncertain decision. |
| Coverage | Fraction of eligible files receiving an automatic accepted decision. |
| Accepted precision | Correct decisions divided by decisions allowed to automate. |
| False rotation | Rotating a file that was already correctly upright or selecting the wrong rotation. |
| Job | Durable directory/database containing manifest, plan, events, reports, and optional review artifacts. |
| Sealed plan | Immutable set of planned file actions and fingerprints produced by scan. |
| Stale plan | A plan whose source identity/content or governing compatibility no longer matches apply-time state. |
| Safe-copy | Write corrected outputs under a separate output root; default mode. |
| In-place | Replace a source path after backup, verification, journal, and explicit acknowledgement. |
| DCT-domain transform | JPEG coefficient-level quarter-turn/flip that avoids decode-and-lossy-reencode. |
| MCU | Minimum coded unit; JPEG sampling geometry can make lossless edge handling imperfect for some dimensions. |
| Capability gate | Preflight determination that a format/filesystem/runtime supports the exact requested operation. |
| Reason code | Stable machine-readable explanation for a decision, refusal, warning, or failure. |
| Model bundle | ONNX file plus manifest, digest, compatibility, provenance, license, and calibration references. |
| External contract | CLI grammar, exit codes, JSON/JSONL, config/schema, job/review/model formats, or installer behavior consumed outside a component. |
