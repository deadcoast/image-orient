# WP-003 context packet — configuration and schemas

Generated 2026-07-23 per DOC-DEVPLAN ("Work-package context packet").

## Objective

Implement DOC-CONFIG: deterministic five-layer precedence (CLI > environment
`UPRIGHT_*` > explicit `--config` > user file > built-in defaults), strict
validation against `schemas/config.schema.json` semantics before any work
begins, per-setting provenance, a canonical effective-config digest (consumed
later by plan sealing), and working `config init/show/validate` commands with
`config show --effective --redacted --json` as the canonical support view.

## Mapped requirements and acceptance

- **PRD-FR-019** (Must) — AC-036 (precedence), AC-037 (unknown/invalid fields
  fail before processing; no forward-compatible mutation mode exists yet, so
  strict is the only behavior).
- **NFR-MAINT-002** versioned schemas (ADR-0010: `schemaVersion` const 1.0).

## Design constraints (DOC-ARCH layering)

- `OrientExpress.Contracts`: config record tree mirroring the schema, plus
  `EffectiveConfig`/`ConfigIssue` — data only, package-free.
- `OrientExpress.Application`: pure logic — defaults, layer merge with
  provenance, validation mirroring the schema bounds/enums/consts, canonical
  digest, environment-variable mapping. No file or environment access.
- `OrientExpress.Cli`: composition root — file discovery/reading, environment
  snapshot, command handlers. A `HostEnvironment` seam keeps golden tests
  hermetic (no process-global env mutation).

## Upstream inputs

DOC-CONFIG (precedence, env mapping `UPRIGHT_GROUP__KEY`, unknown fields fail
by default, canonical support view); `schemas/config.schema.json` (bounds,
enums, `privacy.telemetry` const false); WP-002 host (exit registry: invalid
configuration = 21 `INVALID_CONFIGURATION`; grammar vs refusal semantics).

## Threat/risk notes

Config files and env vars are untrusted input: strict unknown-field
rejection catches typos and injection of unsupported behavior (DOC-CONFIG
"Configuration safety"); `privacy.telemetry` cannot be enabled by any layer
(schema const). Redaction: `config show --redacted` tokenizes path values
before display; nothing writes secrets.

## Test and evidence plan

Application unit tests: precedence order (AC-036), group and leaf merging,
provenance attribution, every validator rule class (enum, range, const,
pattern, schemaVersion), env parsing including malformed values, digest
stability and sensitivity. CLI golden tests: `config validate` exit 0/21,
`config init` create/refuse/`--force`, `config show --json` effective
document with provenance and digest, `--profile` (CLI layer) beating file
and env layers, env beating explicit file, unknown config field fails with
exit 21 before any processing (AC-037).

## Dependencies and consumers

No new packages. Consumers: every command WP (effective config), WP-009
(sealed config digest), WP-018 (support view).

## Out of scope

Job-sealed config enforcement (WP-009); `--allow-unknown-config` (no
read-only consumer exists yet); config-driven path creation; profiles beyond
`analysis.profile`; localization.

## Definition of done

Validator/build/format/tests green; AC-036 implemented and AC-037
implemented for the strict path; statuses, DOC-STATUS, CHANGELOG, manifest
updated; traceability intact.
