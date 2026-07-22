---
document_id: DOC-TECH
title: Technology Evaluation and Baseline Stack
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-LANDSCAPE, DOC-SOURCES]
traces_to: [ADR-0002, ADR-0004, ADR-0006, ADR-0007, ADR-0008]
---

# Baseline stack

Versions shown in the planning catalogs are snapshots and must be pinned to current supported releases at implementation kickoff.

| Area | Baseline | Alternative | Decision rule |
|---|---|---|---|
| Language/runtime | C# / .NET 10 LTS | Rust/C++, Python | Choose .NET for Windows APIs, deployment, testability, and maintainability; native code remains behind adapters |
| CLI parser | System.CommandLine stable 2.x | Cocona, CliFx, custom | Stable grammar/completion/validation; contract tests isolate library |
| Human rendering | Spectre.Console, isolated | Plain console | Optional only; machine stdout must remain pure |
| Inference API | `IInferenceRuntime` with ONNX tensors | Framework-specific APIs | Domain cannot import provider-specific types |
| Preferred runtime | Windows ML self-contained | Direct ORT | SPIKE-003 decides provider and deployment defaults |
| Deterministic fallback | Direct ONNX Runtime CPU | OpenCV DNN | Must run offline and preserve decision provenance |
| Face model | YuNet-class ONNX | MediaPipe/RetinaFace/custom | Meet pilot precision/latency/license/package gates |
| Metadata | ExifTool `-stay_open` adapter | MetadataExtractor + WIC/native writer | Breadth and round-trip matrix decide; adapter remains replaceable |
| JPEG transform | libjpeg-turbo/jpegtran | TurboJPEG transform API | DCT lossless path, edge policy, marker behavior, packaging |
| General raster | WIC and/or Magick.NET | ImageSharp, libvips binding | Format matrix, security, metadata, package size, licensing |
| Job store | Microsoft.Data.Sqlite | LiteDB/files | Transactions, integrity, scale, migrations, supportability |
| Unit/integration tests | xUnit v3 | NUnit/MSTest | Current .NET support and tooling |
| Microbenchmark | BenchmarkDotNet | custom Stopwatch | Use only for controlled component performance |
| E2E/fault/evaluation | Custom harnesses | — | Required because generic unit tools cannot prove file safety or ML quality |
| Packaging | self-contained win-x64 zip + signed installer | MSIX only | Offline clean-VM, rollback, enterprise and user experience gates |
| AOT | Not promised | NativeAOT | Qualify after native/model/process dependencies are stable |

## Why not Python as the product runtime?

Python is excellent for corpus exploration and model analysis, and may be used in development tooling. For a high-end Windows CLI, a C# host gives stronger single-package deployment, Windows handle/API integration, typed contracts, and enterprise-friendly operation. This does not mean Python prototypes are discarded; spike code can establish algorithms and fixtures before production reimplementation.

## Dependency policy

- Central version management and lock files.
- Exact native/model digests in release manifests.
- No download during core operation.
- Optional model/provider installation is explicit, authenticated, and rollback-safe.
- Dependency updates run full contract, corpus, metadata, JPEG, fault, and compatibility suites.
