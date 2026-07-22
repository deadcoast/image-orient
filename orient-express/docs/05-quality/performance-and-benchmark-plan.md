---
document_id: DOC-PERF
title: Performance, Capacity, and Benchmark Plan
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Performance
depends_on: [DOC-TEST, DOC-PIPELINE]
traces_to: [PRD-FR-022, PRD-FR-026, NFR-PERF-001, NFR-PERF-002, NFR-PERF-003, NFR-PERF-004]
---

# Performance plan

## Principles

Performance work cannot change decisions, fidelity, or safety without full regression. Collection size must affect elapsed time and durable storage, not unbounded working memory.

## Reference environments

At least:

- baseline supported Windows 11 x64 laptop, 4–8 cores, 16 GB RAM, integrated GPU, SSD;
- CPU-only or provider-disabled mode;
- higher-end discrete GPU system;
- HDD/external storage case;
- clean VM for cold-start/package measurements.

Record exact hardware, OS build, power mode, provider/driver, filesystem, antivirus state, and package/model versions.

## Workloads

| Workload | Purpose |
|---|---|
| `doctor` cold/warm | Startup and capability probe |
| single `inspect` | Interactive latency |
| metadata-only 1k | ExifTool throughput/framing |
| decode thumbnails by megapixel/format | Memory and decode cost |
| four-candidate inference | Runtime/provider throughput and divergence |
| eligible JPEG transform | DCT throughput and I/O |
| raster PNG/JPEG fallback | Encode cost and memory |
| plan 10k/100k/1m synthetic | discovery/SQLite scale |
| safe-copy E2E mixed corpus | representative throughput |
| cancellation/restart | checkpoint/recovery latency |
| in-place fault suite | safety overhead |
| 8-hour/100k soak | leaks, queue bounds, log/DB growth |

## Metrics

- cold/warm startup;
- p50/p95/p99 per-file stage latency;
- files/minute and megapixels/second;
- CPU/GPU/NPU utilization;
- peak and steady working set;
- managed/native allocations;
- I/O bytes/operations and queue depth;
- queue occupancy/backpressure;
- SQLite write/checkpoint latency and DB/WAL size;
- native subprocess restart count;
- energy/power where available;
- output/job/log bytes per input.

## Proposed initial targets

These are provisional until representative hardware/corpus results exist.

- `doctor` under 3 seconds when dependencies are local.
- single `inspect` p95 under 2 seconds after model availability.
- default working set target ≤1.5 GB on reference host.
- no linear memory growth across 100k files.
- graceful cancellation reaches durable stop within 10 seconds outside indivisible native/commit sections.
- restart scans job integrity and resumes without reprocessing completed rows except explicit verification.

## Benchmark design

BenchmarkDotNet is used for isolated pure/component operations with fixed inputs. It is not used to claim end-to-end or ML corpus performance. The custom harness:

- warms providers explicitly;
- separates model load from steady inference;
- randomizes/counterbalances file order;
- controls OS cache where practical;
- runs enough repetitions for stable intervals;
- reports failures and review decisions, not only successful throughput;
- stores environment and corpus manifests.

## Optimization order

1. Metadata/unsupported fast paths.
2. Bounded thumbnail decode.
3. Avoid duplicate reads/digests.
4. Persistent metadata process.
5. Reuse model sessions and buffers safely.
6. Bounded parallelism by resource.
7. Candidate batching only after equivalence proof.
8. Provider acceleration after decision-equivalence proof.
9. Native/raster optimization.
10. Advanced caches with explicit invalidation/provenance.

## Capacity controls

Users can set max files, bytes, duration, memory, workers, and temp/job storage. Plan reports estimates before apply. The application degrades by lowering concurrency, not by violating memory limits.

## Performance regression gate

Compare candidate to release baseline on fixed hardware/workloads. Investigate >10% median or material p95/memory regression. Safety and quality paths may accept overhead only with documented reason and product approval.
