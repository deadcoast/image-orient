# SPIKE-006 harness — crash-safe atomic replace on Windows

Decision harness for [SPIKE-006](../../docs/06-delivery/technical-spikes.md#spike-006--crash-safe-atomic-replace-on-windows)
(risk [RISK-005]: source loss during in-place replacement). It is **evidence
tooling, not production code**; it lives outside `src/` so the DOC-ARCH layer
rules keep applying to the product tree.

[RISK-005]: ../../planning/risk-register.csv

## What it does

- **Primitive qualification** — measures what `ReplaceFileW` and
  `MoveFileEx(MOVEFILE_REPLACE_EXISTING)` actually preserve on NTFS (content,
  creation time, attributes, alternate data streams, DACL, file identity) and
  which Win32 error codes the locked/read-only/missing/cross-volume cases
  produce. No API name is treated as a guarantee (DOC-FSSAFE).
- **Crash fault matrix** — a child process runs the DOC-FSSAFE in-place
  protocol (verified backup → same-directory stage → flush → durable journal
  intent → `ReplaceFileW` → independent verify → durable commit) and is
  kernel-terminated (`TerminateProcess` on self: no finalizers, no flushes) at
  every step boundary. The parent then observes the filesystem, runs the
  recovery state machine, and checks the **zero-source-loss invariant**: the
  original bytes must remain recoverable from the source or the verified
  backup in every injected case, including stage corruption and external
  tampering.
- **Recovery state machine** — `RecoveryPlanner` is a pure function encoding
  the DOC-FSSAFE recovery matrix; `OrientExpress.Spike006.Tests` proves it
  row by row (test-first) plus totality over the whole observation space, and
  runs in CI with the rest of the solution.

Fixtures are synthetic and deterministic (derived from scenario ids); no user
photos are read or copied, per the SPIKE-001 consent rules.

## Reproduce

```powershell
dotnet build OrientExpress.slnx -c Release
./spikes/SPIKE-006-atomic-replace/OrientExpress.Spike006/bin/Release/net10.0/spike006.exe `
  run-all --results spikes/SPIKE-006-atomic-replace/results/spike006-results.json
```

Optional: `--work <dir>` pins the scratch directory (must be on the volume you
intend to measure). Exit code 0 means no scenario failed and no source-loss
case occurred. Raw machine-readable results, including the environment and
volume manifest, land in [`results/spike006-results.json`](results/spike006-results.json).

## Reading the evidence

- [`report.md`](report.md) — summarized findings, limitations, and the
  recommended decision with alternatives.
- [`results/spike006-results.json`](results/spike006-results.json) — raw
  scenario measurements from the recorded run.

Scenario ids: `P-*` are primitive qualification, `F-*` are fault-injection
cases. `SourceSafetyHeld=false` anywhere is a release-blocking source-loss
case (DOC-FSSAFE safety proof; ADR-0009).
