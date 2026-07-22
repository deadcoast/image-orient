---
document_id: DOC-TEST
title: Test Strategy and Verification Architecture
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Quality
depends_on: [DOC-SRS, DOC-ARCH, DOC-AC]
traces_to: [TEST-AC-001, TEST-AC-002, TEST-AC-003, TEST-AC-004, TEST-AC-005, TEST-AC-006, TEST-AC-007, TEST-AC-008, TEST-AC-009, TEST-AC-010, TEST-AC-011, TEST-AC-012, TEST-AC-013, TEST-AC-014, TEST-AC-015, TEST-AC-016, TEST-AC-017, TEST-AC-018, TEST-AC-019, TEST-AC-020, TEST-AC-021, TEST-AC-022, TEST-AC-023, TEST-AC-024, TEST-AC-025, TEST-AC-026, TEST-AC-027, TEST-AC-028, TEST-AC-029, TEST-AC-030, TEST-AC-031, TEST-AC-032, TEST-AC-033, TEST-AC-034, TEST-AC-035, TEST-AC-036, TEST-AC-037, TEST-AC-038, TEST-AC-039, TEST-AC-040, TEST-AC-041, TEST-AC-042, TEST-AC-043, TEST-AC-044, TEST-AC-045, TEST-AC-046, TEST-AC-047, TEST-AC-048, TEST-AC-049, TEST-AC-050, TEST-NFR-001, TEST-NFR-002, TEST-NFR-003, TEST-NFR-004, TEST-NFR-005, TEST-NFR-006, TEST-NFR-007, TEST-NFR-008, TEST-NFR-009, TEST-NFR-010, TEST-NFR-011, TEST-NFR-012, TEST-NFR-013, TEST-NFR-014, TEST-NFR-015, TEST-NFR-016, TEST-NFR-017, TEST-NFR-018, TEST-NFR-019, TEST-NFR-020, TEST-NFR-021, TEST-NFR-022, TEST-NFR-023, TEST-NFR-024, TEST-NFR-025, TEST-NFR-026, TEST-NFR-027, TEST-NFR-028]
---

# Test strategy

## Quality model

Testing must prove four independent properties:

1. **Decision quality:** accepted orientation choices are correct and calibrated.
2. **Media fidelity:** pixels, dimensions, color, metadata, and JPEG policies behave as promised.
3. **Operational safety:** interruption, concurrency, filesystem anomalies, and retries never lose the last good copy.
4. **Automation compatibility:** CLI, schemas, exit codes, jobs, models, and configuration remain stable.

A unit-test count is not a release metric.

## Test layers

| Layer | Purpose | Representative evidence |
|---|---|---|
| Domain unit | Transform algebra, policy rules, scoring math, state transitions | xUnit deterministic tests |
| Property-based | Dihedral transform composition, path/collision invariants, serialization round trips | Generated cases and seeds |
| Contract | Every port/adaptor obeys typed success/error/cancellation/provenance contract | Shared adapter conformance suites |
| CLI golden | Help, errors, stdout/stderr separation, JSON/JSONL, exit codes, response files | Golden snapshots + schemas |
| Metadata golden | EXIF 1–8, XMP/IPTC, timestamps, maker notes, profiles | Before/after semantic diff |
| Codec/media golden | JPEG sampling/dimensions, PNG profiles/alpha, corrupt/adversarial images | Pixel/coefficient/property checks |
| ML component | Candidate generation, feature extraction, calibration, reason codes | Labeled feature fixtures |
| Corpus evaluation | Precision, false rotation, coverage, calibration and slices | Signed evaluation report |
| Persistence | DB transactions, seals, migration, integrity, append-only behavior | Crash/restart and corruption cases |
| Filesystem fault | Crash points, locks, disk-full, alias, antivirus-like interference | Fault matrix with source/backup digests |
| End-to-end | Real commands on clean temporary roots | Job/report/output assertions |
| Compatibility | OS/filesystem/codec/provider/PowerShell matrix | Environment manifests |
| Performance/soak | Throughput, latency, memory, queue bounds, restart | Benchmark and 100k-file reports |
| Security/fuzz | Parser/native boundary robustness, malicious manifests/images/jobs | Fuzz corpora and findings |
| Packaging | Clean VM install/run/update/uninstall/offline, signatures/SBOM | Package verification report |

## Test repositories and fixture policy

- Small synthetic/golden fixtures may be committed when licensing/privacy permit.
- Personal-photo corpus stays in an access-controlled external store with dataset manifests/digests.
- Public benchmark files retain source/license records.
- Corrupt/adversarial fixtures are minimized and tagged.
- Every fixture states expected metadata, dimensions, transform, and permitted variation.
- Tests never silently rewrite golden files in CI.

## Determinism

Test reports record tool build, OS, CPU/GPU/NPU, runtime/provider, model/calibration/policy digests, native tool versions, locale/timezone, and configuration. Randomized tests store seeds. ML comparisons use tolerances and, more importantly, verify that numeric drift does not cross decision gates.

## Architecture tests

Automated rules prevent:

- Domain reference to Infrastructure/Presentation.
- Application reference to concrete adapter packages.
- CLI direct process invocation or SQLite access.
- Infrastructure types leaking into public contracts.
- mutating service use from scan command handlers.
- decision-policy access to file writes/network/console.
- arbitrary plugin assembly loading.

## Fault-injection strategy

Inject at every durable boundary:

- before/after backup create, flush, and verify;
- before/after stage write, metadata update, flush, and verify;
- before/after journal intent commit;
- immediately before/during/after replace;
- before/after final reopen and completion commit;
- during cleanup;
- corresponding undo boundaries.

Faults: process kill, exception, simulated reboot recovery, disk-full, access denied, sharing violation, disappearing volume, modified source/destination, corrupted stage/backup, DB lock/corruption, native-tool timeout/crash.

For each case, assert that at least one verified source/backup copy remains and recovery is deterministic.

## Media fidelity checks

### JPEG DCT path

- dimensions and sampling;
- quantization/Huffman/coefficient equivalence as appropriate;
- no unplanned crop;
- marker/metadata policy;
- orientation normalization;
- decode equivalence under reference decoder;
- repeated rotate/inverse test where mathematically applicable.

### Raster path

- expected dimensions/pixel geometry;
- bit depth/alpha/color profile;
- compression policy;
- required metadata;
- no extra frame/page loss.

## Contract compatibility

The previous supported release's contract fixtures run against the current candidate:

- CLI invocations;
- exit codes;
- JSON/JSONL schemas/examples;
- config;
- job open/read/migrate;
- model manifest;
- review import;
- reason codes.

## Test result policy

Every test produces machine-readable JUnit/TRX/JSON or an evidence manifest. Release gates link to immutable build artifacts. Flaky tests are quarantined only with owner, issue, expiration, and non-safety classification; safety/ML release gates cannot be waived as “flaky.”

## Canonical mapping

[`planning/test-matrix.csv`](../../planning/test-matrix.csv) maps each acceptance criterion and NFR to evidence. CI validates that every Must functional requirement has acceptance and test coverage.
