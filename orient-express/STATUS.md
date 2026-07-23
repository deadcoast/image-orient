---
document_id: DOC-STATUS
title: orient-express Planning Status
version: 0.1.9
status: baseline
as_of: 2026-07-23
owner: Program
depends_on: [DOC-DEVPLAN, DOC-SPIKES, DOC-WBS]
traces_to: []
---

# Planning status

## Current state

The project is at **engineering baseline / pre-implementation**. Product intent, requirements, architecture, risk model, technical spikes, work packages, milestones, draft schemas, and traceability structure are defined. WP-001 is implemented pending CI verification and review: the `OrientExpress` solution and project boundaries exist with architecture conformance tests (NFR-MAINT-001), analyzers/formatting/warnings-as-errors, deterministic pinned builds with locked packages, and a CI pipeline that gates documentation, build, style, and tests. No production feature code or measured accuracy claim is represented as complete.

SPIKE-006 is **complete** ([report](spikes/SPIKE-006-atomic-replace/report.md)): the DOC-FSSAFE recovery state machine is proven on NTFS by unit tests and a twelve-case crash fault matrix with zero source-loss cases, and the atomic primitives are measured (notably: `ReplaceFileW` preserves creation time, attributes, ADS, and DACL, but not the NTFS file ID — identity re-baselines after commit). Its exit gate is met with non-NTFS classes capability-gated; ADR-0009, DOC-FSSAFE, and DOC-WBS carry the consequences. In-place mode still ships only after the full fault suite across supported compatibility classes and the undo path (WP-015 gate).

SPIKE-009 is **complete** ([report](spikes/SPIKE-009-cli-contract/report.md)): the DOC-CLI machine contract, exit-code registry, destructive-flag guard, sealed-plan staleness refusal, response files, completion, and cancellation are validated on System.CommandLine 2.0.10 (DEP-002) with 20 CI golden tests and 11 out-of-process byte-level evidence scenarios, all passing. DOC-CLI 0.1.1 records the binding contract decisions; registry numerics freeze at WP-002 entry. WP-002 and WP-018 are unblocked on their spike gate.

SPIKE-008 is **complete** ([report](spikes/SPIKE-008-job-store/report.md)): the ADR-0008 proof gate is met at 100k and 1m synthetic rows — batched inserts 162k–185k rows/s, 1m-row restart in 43 ms, migration in 243 ms, integrity ok after kernel-kill crash injection with exactly the committed rows surviving. ADR-0008 is now **accepted**; the DOC-DATAMODEL constraints are enforced as schema triggers and proven by CI tests. Dependency intake caught and pinned past a vulnerable SQLitePCLRaw transitive (DEP-018, GHSA-2m69-gcr7-jv3q). WP-009's spike gate is satisfied.

SPIKE-001 is **in progress** ([harness](spikes/SPIKE-001-corpus/README.md)): the opt-in read-only inventory scanner (aggregates only; EXIF orientation histogram, formats, buckets) is verified by CI tests and a synthetic self-test, and the truth protocol (labeling guide, adjudication, kappa measurement, dataset cards, slice taxonomy) is drafted. The exit gate now waits only on operator-owned photos: scanner runs over opted-in trees, a stratified consented tranche, and double labeling. This is the sole remaining blocker for SPIKE-002/003 and the M2 ML work.

**WP-002 (CLI host and contracts) is implemented** — the first production feature code ([context packet](planning/context-packets/WP-002-context-packet.md)). `upright` now hosts the full DOC-CLI command tree on System.CommandLine: working `version`, `doctor` (schema-shaped capability report with real platform probes), and `completion powershell`; frozen exit-code registry (`cli-contract/1.0`, DOC-CLI 0.2.0) in `OrientExpress.Contracts`; machine JSON/JSONL streams per the event schema; response files; cancellation wiring; and every not-yet-proven command honestly refusing with `22 CAPABILITY_UNAVAILABLE` and a consumable refusal document. 44 CLI golden tests run in CI. AC-033/034 implemented; AC-003/035/048 partially implemented (process-level sweeps land with WP-018/CI).

**WP-003 (configuration and schemas) is implemented** ([context packet](planning/context-packets/WP-003-context-packet.md)): DOC-CONFIG's five-layer precedence (CLI > `UPRIGHT_*` environment > `--config` > user file > defaults) with per-setting provenance, strict schema-mirroring validation that fails closed before any work (AC-036/AC-037 implemented), the canonical effective-config digest that WP-009 will seal into plans, `privacy.telemetry` unoverridable by any layer, and working `config init/show/validate` — including `config show --redacted --json` as the canonical support view. Pure precedence/validation logic lives in `OrientExpress.Application`; the host gained a hermetic environment seam for testing.

**WP-004 (filesystem and identity foundation) is implemented** ([context packet](planning/context-packets/WP-004-context-packet.md)): the DOC-COMPONENTS `Result<T>` model and `IFileIdentityService` port, handle-based snapshots with standard/strong fingerprints and drift detection, extended-length Unicode path canonicalization (NFR-COMP-002), reparse/cloud-placeholder policy refusals, NFR-COMP-001 volume capability classes (NTFS-fixed supported; network/removable/non-NTFS gated), same-directory stage planning, and the SPIKE-006-qualified atomic primitives with typed error classes. `upright doctor --probe-target <dir>` now runs live filesystem probes — including demonstrating the post-replace file-id re-baseline — and cleans up after itself. Real-NTFS integration tests cover identity, hardlink aliases, long paths, and locked/missing/tampered cases.

The project operates under single-operator governance: the documentation gates (validator, CI, tests, evidence rules) are the control surface; multi-role review sign-offs are recorded as the operator's own acceptance.

## Approved to begin

- Repository and CI baseline.
- Corpus governance and truth protocol.
- Read-only prototypes for metadata, decode, model runtime, and file identity.
- All ten risk-first technical spikes.
- CLI contract prototype and schema review.

## Blocked until evidence exists

| Capability | Blocking evidence |
|---|---|
| Production orientation thresholds | SPIKE-001 and SPIKE-002 reports |
| Selected inference provider | SPIKE-003 report and ADR update |
| Metadata guarantee language | SPIKE-004 round-trip matrix |
| Default JPEG edge policy | SPIKE-005 report |
| In-place mode | Full fault suite across supported compatibility classes plus undo-path evidence (SPIKE-006 NTFS pilot complete 2026-07-23) |
| HEIC/AVIF claims | SPIKE-007 capability matrix |
| Final job-store settings | Settled: WAL + NORMAL + batched single-writer + trigger constraints (SPIKE-008 complete 2026-07-23) |
| Frozen CLI v1 contract | Numeric registry freeze at WP-002 entry (SPIKE-009 automation evidence complete 2026-07-23; usability feedback accumulates from the M1 alpha) |
| Signed distribution shape | SPIKE-010 clean-VM proof |

## Next controlled execution sequence

1. Keep WP-001 CI documentation/architecture checks green (baseline established 2026-07-22).
2. Run SPIKE-001, SPIKE-004, SPIKE-005, SPIKE-006, and SPIKE-009 in parallel where ownership permits.
3. Use the first corpus tranche from SPIKE-001 to run SPIKE-002 and SPIKE-003.
4. Run codec/package spikes before selecting final native distribution.
5. Update provisional ADRs with evidence and only then unlock M1/M2 implementation packages.

See [technical spikes](docs/06-delivery/technical-spikes.md) and [work breakdown](docs/06-delivery/work-breakdown-structure.md).
