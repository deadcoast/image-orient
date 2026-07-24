# SPIKE-002/SPIKE-003 harness — YuNet orientation proof and inference runtime

Shared harness for
[SPIKE-003](../../docs/06-delivery/technical-spikes.md#spike-003--inference-runtime-and-provider-qualification)
(inference runtime qualification, DEP-005/ADR-0004 — **complete**) and
[SPIKE-002](../../docs/06-delivery/technical-spikes.md#spike-002--four-rotation-yunet-orientation-proof)
(four-rotation orientation proof, DEP-006/ADR-0005 — **pilot passing;
precision gate awaits the corpus tranche**).

## Digest-pinned artifacts (never committed, verified on every use)

| Artifact | Source | SHA-256 | License/provenance |
|---|---|---|---|
| `yunet.onnx` (232,589 B) | OpenCV Zoo `face_detection_yunet_2023mar` | `8f2383e4dd3cfbb4553ea8718107fc0423210dc964f9f4280604804ed2552fa4` | MIT (Shiqi Yu) |
| `portrait1.jpg` | Wikimedia Commons, official 2012 presidential portrait | `744dd848fbb0584229169e01c4944664957c62495fb9e8af514a088ebca43e19` | US federal government work — public domain |

Cache: `%LOCALAPPDATA%\orient-express\spike-fixtures\`. `Artifacts.Require`
refuses to run on a digest mismatch.

## What it does

- **YuNetDetector**: ONNX Runtime CPU session; fixed 640×640 BGR 0–255
  input; decodes the twelve raw heads (cls/obj/bbox/kps at strides 8/16/32)
  per the OpenCV FaceDetectorYN post-processing; NMS; maps boxes and the
  five landmarks back to source coordinates.
- **SPIKE-003 evidence**: model load, cold/warm latency distribution,
  bitwise determinism within and across sessions, native footprint,
  provider policy.
- **SPIKE-002 pilot**: presents the portrait at 0/90/180/270 and picks the
  counter-rotation with the highest face score (the four-candidate
  approach), plus the eyes-above-mouth geometric check from the landmarks.
- 6 CI-safe math tests (IoU/NMS/landmarks/rotation/tensor layout) — CI
  runners need no model or network.

## Reproduce

```powershell
dotnet build OrientExpress.slnx -c Release
./spikes/SPIKE-002-orientation-proof/OrientExpress.Spike002/bin/Release/net10.0/spike002.exe `
  run-evidence --results spikes/SPIKE-002-orientation-proof/results/spike002-003-results.json
```

Raw results: [`results/spike002-003-results.json`](results/spike002-003-results.json).
Findings: [`report.md`](report.md) (SPIKE-002 pilot) and
[`../SPIKE-003-inference-runtime/report.md`](../SPIKE-003-inference-runtime/report.md).
