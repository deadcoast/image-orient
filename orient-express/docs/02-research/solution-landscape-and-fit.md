---
document_id: DOC-LANDSCAPE
title: Solution Landscape and Fit Assessment
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Research
depends_on: [DOC-RESEARCH, DOC-SOURCES]
traces_to: [ADR-0004, ADR-0005, ADR-0006, ADR-0007, ADR-0008]
---

# Solution landscape

| Asset/approach | Useful capability | Fit | Why it is not the whole product |
|---|---|---|---|
| ImageMagick `-auto-orient` | Applies EXIF display orientation | Reference/possible raster adapter | No visual inference; generic save may change encoding/metadata |
| jhead/exiftran-style tools | EXIF-driven JPEG autorotation using jpegtran | Prior art | JPEG-focused, metadata-trusting, limited job/review/recovery contracts |
| ExifTool | Broad metadata inspection/write/copy; persistent process mode | Strong candidate behind port | Does not infer visual orientation or transform pixels; subprocess/license/round-trip work remains |
| libjpeg-turbo/jpegtran | DCT-domain quarter-turns and marker copying | Strong candidate | Requires explicit MCU edge policy and metadata orchestration |
| Windows Imaging Component | Windows codec/metadata discovery | Candidate capability/fallback layer | Installed codec behavior varies; not a complete metadata or lossless-JPEG solution |
| Magick.NET/ImageMagick | Broad raster operations and formats | Candidate general adapter | Larger native/delegate/security surface; must constrain policies and verify metadata |
| YuNet/OpenCV model | Small face detector with five landmarks | Baseline ML candidate | Face detection benchmark is not upright-orientation proof |
| MediaPipe face tasks | Mature local face/landmark pipeline | Reserve candidate | More integration/model surface than required; benchmark before substitution |
| RotNet-like scene rotation models | Can learn global orientation cues | Research fallback | Dataset shortcuts and artistic images require domain-specific calibration |
| Windows ML | Local ONNX with Windows execution-provider ecosystem | Preferred runtime candidate | Windows/version/provider/package behavior requires qualification |
| Direct ONNX Runtime | Controlled local inference, CPU baseline | Required fallback candidate | Hardware provider management becomes application responsibility |
| SQLite | Durable local jobs and event history | Strong candidate | Schema, WAL, migration, and large-job behavior must be proven |
| Simple one-pass scripts | Easy to prototype | Useful spike harness only | Weak audit, recovery, safety, compatibility, and machine-contract story |
| Desktop photo managers | Manual rotate/review UI | Workflow inspiration | Broad catalog products; poor headless automation and no dedicated inference safety contract |

## Adopt / adapt / reject

### Adopt after qualification

ONNX model contract, compact face detector, ExifTool adapter, libjpeg-turbo lossless transform, SQLite job store, stable .NET CLI ecosystem.

### Adapt behind ports

Windows ML versus direct ORT, WIC versus Magick.NET, model artifact/input size, provider acceleration, installer technology, optional scene model.

### Reject for v1

Blind EXIF-only logic as the complete solution, aspect-ratio classification, forced four-way prediction without abstention, generic re-save of every JPEG, destructive one-pass defaults, cloud inference, face identity features, arbitrary in-process plugins, and unversioned JSON output.
