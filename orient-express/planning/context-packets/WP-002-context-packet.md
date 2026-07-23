# WP-002 context packet — CLI host and contracts

Generated 2026-07-23 per DOC-DEVPLAN ("Work-package context packet").

## Objective

Replace the `upright` stub with the production System.CommandLine host
implementing the DOC-CLI contract in **diagnostic-mode scope**: full command
tree, global output options, schema-shaped machine streams, frozen exit-code
registry, response files, model-derived PowerShell completion, cancellation
wiring, and honest capability gating for every command whose subsystem is not
yet proven.

## Mapped requirements and acceptance

- **PRD-FR-002** (partial: `doctor` only; `inspect` needs WP-004/005) — AC-003.
- **PRD-FR-017** machine/human output — AC-033, AC-034.
- **PRD-FR-018** stable exit codes — AC-035 (partial: process-level
  PowerShell/cmd.exe sweep completes at WP-018/CI).
- **PRD-FR-027** completion + response files — AC-048.
- **NFR-UX-001** destructive flags (no destructive path exists yet; the gate
  pattern reserves them), **NFR-UX-002** decoration-free machine output.

## Upstream spike conclusions (binding)

From [SPIKE-009](../../spikes/SPIKE-009-cli-contract/report.md) via DOC-CLI 0.1.1:
exit aggregation order; grammar errors keep machine stdout empty (stderr +
exit 20); host must reject option-like tokens bound into greedy positionals;
machine stdout is UTF-8-without-BOM via explicit console encoding; non-ASCII
is `\uXXXX`-escaped and decoded values are the contract; response files are
host-expanded UTF-8, one argument per line, `#` comments, no nesting.
**Registry numerics freeze now, at WP-002 entry** (DOC-CLI 0.2.0).

## ADRs and contracts

ADR-0001 (layering: contract types in `OrientExpress.Contracts`, host in
`OrientExpress.Cli`); ADR-0003 (scan seals / apply consumes shapes the gated
command surface); ADR-0010 (contract versioning: `cli-contract/1.0`).
Schemas: `schemas/event.schema.json`, `schemas/capability-report.schema.json`.

## Threat/risk notes

RISK-014 (unsafe one-command expectations): gated commands refuse with
`CAPABILITY_UNAVAILABLE`, never simulate success. Response-file paths are
attacker-adjacent input: read as UTF-8 text only, no nesting, no globbing.
No network access anywhere in the host.

## Test and evidence plan

`tests/OrientExpress.CliTests` (CI): registry numeric freeze; machine-stream
purity and schema shape for `version`/`doctor`; capability-report structural
conformance (AC-003); gated-command refusal semantics; grammar-error classes
(unknown option, option-like positional, conflicting format flags, bad
values); response files; completion determinism; correlation-id propagation;
cancellation exit path. Byte-level encoding evidence remains covered by the
SPIKE-009 out-of-process harness.

## Dependencies and consumers

DEP-002 System.CommandLine 2.0.10 (pinned). Downstream consumers: WP-003
(config binds into the host), WP-018 (operational UX), every command WP.

## Out of scope

Real `scan`/`apply`/`inspect`/`review`/`jobs`/`models`/`verify`/`undo`/
`benchmark` behavior (gated on their WPs/spikes); configuration precedence
(WP-003); `--log` sink (accepted, recorded, not yet persisted); Spectre
rendering (DEP-003 stays a candidate); localization.

## Definition of done

Validator, build (0 warnings), format, and all tests green; commands that
cannot act refuse honestly with stable reason codes and next actions; no
dead safety paths; DOC-CLI, backlog, STATUS, CHANGELOG, and manifest updated;
traceability intact.
