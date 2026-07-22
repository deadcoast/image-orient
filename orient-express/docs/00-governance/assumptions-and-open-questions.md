---
document_id: DOC-ASSUMPTIONS
title: Assumptions and Open Questions
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-RISKS]
traces_to: []
---

# Assumptions

| ID | Assumption | Validation |
|---|---|---|
| ASM-001 | The majority of target images contain at least one sufficiently visible human face. | SPIKE-001 corpus inventory |
| ASM-002 | Five-point landmarks across four rotations provide strong enough upright evidence for high-precision automation. | SPIKE-002 |
| ASM-003 | Metadata-first handling resolves a material fraction without visual inference. | SPIKE-001 and SPIKE-004 |
| ASM-004 | A self-contained offline Windows package is acceptable even with native/model payload size. | SPIKE-010 |
| ASM-005 | A local SQLite job store can sustain target collection sizes with bounded resources. | SPIKE-008 |
| ASM-006 | Windows 11 x64/NTFS can provide the required replacement semantics when correct primitives and journaling are used. | SPIKE-006 |
| ASM-007 | Users prefer conservative REVIEW items over occasional wrong automatic rotations. | Product usability study / pilot |

# Open questions

| ID | Question | Owner | Closure |
|---|---|---|---|
| OQ-001 | Which YuNet artifact/input size/runtime combination gives the best calibrated precision/latency? | ML Platform | SPIKE-002/003 |
| OQ-002 | Is a scene-orientation fallback needed for release one? | ML/Quality | SPIKE-002 and evaluation ablation |
| OQ-003 | What is the safest default for non-perfect JPEG DCT transforms: abort or re-encode? | Media I/O/Product | SPIKE-005 |
| OQ-004 | Which metadata fields are guaranteed, best-effort, normalized, or intentionally removed per format? | Media I/O | SPIKE-004 |
| OQ-005 | Should optional HEIC support rely on WIC, Magick.NET delegates, or both? | Media I/O/Release | SPIKE-007 |
| OQ-006 | Does DirectML/provider acceleration preserve decision equivalence closely enough to enable by default? | ML Platform | SPIKE-003 |
| OQ-007 | What backup retention and disk-space preflight policy best balances safety and usability? | Product/Core | SPIKE-006 pilot |
| OQ-008 | Should `run` merely compose `scan` + `apply`, or remain omitted until workflows prove safe? | Product | SPIKE-009 |
| OQ-009 | Which installer technology and Authenticode process best fit distribution? | Release | SPIKE-010 |
| OQ-010 | Is an external-process plugin protocol needed after v1? | Architecture | Post-v1 evidence |
