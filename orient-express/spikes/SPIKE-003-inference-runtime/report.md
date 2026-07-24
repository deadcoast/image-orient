# SPIKE-003 report — inference runtime and provider qualification

**Status:** complete — accepted under single-operator governance (2026-07-23)
**Run:** 2026-07-23, Windows 11 (build 10.0.26200), .NET 10.0.8, x64 CPU; Microsoft.ML.OnnxRuntime 1.27.1 (DEP-005); YuNet 2023mar digest-pinned (DEP-006)
**Raw evidence:** [`../SPIKE-002-orientation-proof/results/spike002-003-results.json`](../SPIKE-002-orientation-proof/results/spike002-003-results.json) · **Harness:** [`../SPIKE-002-orientation-proof/README.md`](../SPIKE-002-orientation-proof/README.md)

## Question and exit gate

Which runtime/provider is the deterministic, offline, supportable baseline
for local ONNX inference? Gate: ADR update selects the baseline
runtime/provider policy with package-size, support, deterministic-result,
and performance evidence.

## Results — CPU baseline selected, all expectations pass

| Measurement | Value |
|---|---:|
| Model load (232 KB YuNet) | 69 ms |
| Cold first inference (640×640) | 14 ms |
| Warm latency, median / p95 (50 runs) | **1.64 ms / 1.96 ms** |
| Determinism (bitwise, within + across sessions) | **exact** |
| Native runtime footprint (`onnxruntime.dll`) | 15.5 MB |
| Offline operation | yes — local NuGet + digest-pinned model; no network at runtime |

**Selected policy (ADR-0004):** direct ONNX Runtime with the **CPU execution
provider** is the v1 baseline. At ~1.6 ms per 640×640 four-candidate pass
(≈6.6 ms per image for all four candidates), CPU throughput is far beyond
the NFR-PERF envelope for a batch photo tool, with bitwise determinism —
which GPU providers cannot generally promise. DirectML and Windows ML
remain *gated alternatives*: they may be benchmarked at WP-021 on real
hardware spread, but nothing in v1 requires them.

## Limitations

- Single machine (x64, one CPU family); WP-021 soak covers hardware spread.
- DirectML/Windows ML not measured (recorded as gated, not rejected).
- Session-option tuning (threads, arenas) deferred to WP-006 defaults.

## Affected items

- **ADR-0004 0.1.1** — provisional → accepted (CPU baseline policy).
- **DEP-005** — Microsoft.ML.OnnxRuntime 1.27.1 pinned centrally; selected.
- **DEP-006** — YuNet 2023mar digest-pinned (MIT); model intake rule: digest
  verified on every load, no mutable "latest".
- **planning/spike-register.csv** — SPIKE-003 → complete.

## Spike completion checklist (DOC-SPIKES)

- [x] Falsifiable question and thresholds declared before the run.
- [x] Inputs documented — synthetic deterministic tensor + digest-pinned model.
- [x] Harness repeatable from a clean checkout (artifact fetch documented).
- [x] Raw output machine-readable.
- [x] Failure cases included (digest mismatch refusal; fixed-input-shape finding).
- [x] Recommendation compares alternatives (CPU vs DirectML/Windows ML with rationale).
- [x] ADR and dependency register updated — ADR-0004 0.1.1; DEP-005/DEP-006.
- [x] Follow-on work package plans updated — WP-006 inputs recorded in DOC-WBS.
- [x] Artifact retained under the evidence policy (results committed; binaries cached, digest-pinned).
