# SPIKE-009 harness — CLI contract and automation ergonomics

Decision harness for [SPIKE-009](../../docs/06-delivery/technical-spikes.md#spike-009--cli-contract-and-automation-ergonomics):
qualifies the DOC-CLI command contract and DEP-002 (System.CommandLine 2.0.10).
It is **evidence tooling, not the WP-002 CLI host**; outcomes are simulated
deterministically from path-token markers (`rot90`, `review`, `error`, …) so
no image code is involved.

## What it does

- **Prototype command slice** — `version`, `doctor`, `inspect`, `scan`,
  `apply`, `completion powershell` with the DOC-CLI global options
  (`--output-format`/`--json`/`--jsonl`, `--no-color`, `--verbosity`,
  `--correlation-id`, `--non-interactive`).
- **Machine streams** — JSONL events matching `schemas/event.schema.json`
  (camelCase, five required fields, additive-only) and a single
  `command.result` document in JSON mode; diagnostics and progress go to
  stderr only.
- **Exit-code registry** — the DOC-CLI proposed registry as a typed enum with
  a documented aggregation rule (failed > review > success > capability >
  nothing-to-do), exercised for 0/10/11/12/20/22/30/40.
- **Safety contract** — `scan` seals a digest-protected plan; `apply` refuses
  tampered plans (`STALE_PLAN`) and refuses in-place plans without
  `--acknowledge-in-place` (`IN_PLACE_NOT_ACKNOWLEDGED`) before any work.
- **Automation ergonomics** — `@file` response files (UTF-8, one argument per
  line, `#` comments), deterministic PowerShell completion generated from the
  command model, simulated cancellation exiting `40 CANCELLED`.
- **Golden tests** — 20 in-process contract tests run in CI; 11 out-of-process
  evidence scenarios measure byte-level behavior (BOM, strict UTF-8, stream
  separation) that in-process tests cannot see.

## Reproduce

```powershell
dotnet build OrientExpress.slnx -c Release
./spikes/SPIKE-009-cli-contract/OrientExpress.Spike009/bin/Release/net10.0/spike009.exe `
  run-evidence --results spikes/SPIKE-009-cli-contract/results/spike009-results.json
```

Exit code 0 means every evidence scenario passed. Raw machine-readable
results: [`results/spike009-results.json`](results/spike009-results.json).
Findings and the recommended contract decisions: [`report.md`](report.md).
