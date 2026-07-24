---
document_id: DOC-CLI
title: CLI User Experience and Command Contract
version: 0.2.1
status: baseline
as_of: 2026-07-24
owner: Product
depends_on: [DOC-WORKFLOWS, DOC-ARCH, DOC-CONFIG]
traces_to: [PRD-FR-002, PRD-FR-016, PRD-FR-017, PRD-FR-018, PRD-FR-019, PRD-FR-027, NFR-UX-001, NFR-UX-002]
---

# CLI UX specification

## Command tree

```text
upright
  doctor
  inspect <file>
  scan <paths...>
  apply <job>
  run <paths...>                 # convenience composition; may remain provisional
  review <job>
    list
    show <file-id>
    decide <file-id>
    export
    import
  verify <job>
  undo <job>
  jobs
    list
    show <job>
    integrity <job>
    prune
  models
    list
    verify
    install
    activate
    rollback                     # additive (WP-006): PRD-FR-020 rollback-safe activation
    remove
  config
    init
    show
    validate
  completion <powershell>
  benchmark
  version
```

## Global options

| Option | Meaning |
|---|---|
| `--config <path>` | Explicit configuration file. |
| `--profile <name>` | Named profile within config. |
| `--output-format human|json|jsonl` | Output contract; aliases `--json`, `--jsonl`. |
| `--no-color` | Disable ANSI/style. Automatic when redirected. |
| `--verbosity quiet|normal|detailed|diagnostic` | Human/diagnostic detail; never changes machine schema. |
| `--log <path>` | Bounded diagnostic log destination. |
| `--correlation-id <id>` | Caller-provided trace value after validation. |
| `--non-interactive` | Refuse prompts; required for deterministic automation. |
| `--help`, `--version` | Standard behavior. |

## `doctor`

Checks without image mutation:

- OS/runtime/architecture;
- long path settings;
- codec operations by format;
- ExifTool/native-tool versions/digests;
- active model/runtime/provider/calibration;
- writable job/cache roots;
- filesystem operation probes when explicit target supplied;
- optional network-disabled readiness;
- license/notice inventory presence.

`doctor --json` is suitable for support and CI preflight.

## `inspect`

Reads one file and emits:

- file identity and format;
- stored dimensions;
- raw EXIF Orientation and displayed dimensions;
- four candidate summaries;
- selected decision/confidence/margin/reasons;
- exact proposed pixel/metadata action;
- JPEG/format fidelity route;
- blockers and next action.

No sidecar or thumbnail is written unless `--export-thumbnail` is explicit.

## `scan`

Important options:

```text
--job <dir>                     required unless --ephemeral is explicitly supported
--output <dir>                  required for safe-copy plan
--mode safe-copy|in-place       default safe-copy
--backup-root <dir>             required for in-place
--include <glob> / --exclude <glob>
--recurse / --no-recurse
--follow-reparse-points         default false; expert
--on-collision error|skip|rename
--jpeg-edge abort|preserve-partial|trim|reencode
--review-threshold <profile>    stable named profiles, not arbitrary raw score by default
--write-review-thumbnails       opt-in
--fingerprint standard|strong
--max-files / --max-bytes       guardrails
```

Scan prints/returns estimated counts, bytes, backup/stage need, actions, review, unsupported, errors, and active policy versions. The sealed plan is the primary output.

## `apply`

Requires a sealed compatible job. Options can restrict work (`--only accepted`, `--only-file-id`) but cannot change planned transforms or weaken fidelity/safety policy. In-place additionally requires `--acknowledge-in-place`. `--resume` is idempotent and reconciles journal first.

## `run`

`run` is equivalent to `scan` then, only when scan seals successfully and policy permits, `apply`. It defaults to safe-copy and should be withheld from v1 if user tests show it obscures the two-phase safety model.

## Review

`review list` supports filters, sort, and JSONL. `review show` can open a generated local HTML/contact sheet only through an explicit export in a future enhancement; the CLI contract itself is headless. `decide` requires an enum and records an append-only override. Import validates job ID, plan-row digest, action, and duplicate/conflict semantics.

## Human-output design

- First line names command/job and mode.
- Present policy/capability warnings before counts.
- Use compact tables for terminal summaries.
- Every error includes stable reason code, safe message, affected item, and next action.
- Detailed mode can show evidence; normal mode avoids ML jargon.
- Avoid spinner/progress when redirected or noninteractive.
- Never say “portrait” when the actual action is “rotate 90° clockwise.”

## Machine-output rules

- JSON mode: exactly one UTF-8 JSON document on stdout.
- JSONL mode: one independently parseable UTF-8 event per line.
- No BOM, comments, ANSI, progress, or prose on machine stdout.
- Diagnostics/progress go to stderr.
- Each object includes `schemaVersion`, `eventType` or command-result type, and correlation/job identifiers.
- Paths follow redaction/output policy.
- Unknown additive fields follow schema compatibility rules.

## Exit-code registry (frozen)

**Frozen at WP-002 entry (2026-07-23, `cli-contract/1.0`).** The numeric
values below are implemented in `OrientExpress.Contracts.Cli.CliExitCode`,
guarded by registry-freeze tests, and must never be repurposed within this
contract major version. Additional non-grammar refusal semantics recorded at
implementation: a *gated* command (subsystem evidence pending) exits
`22 CAPABILITY_UNAVAILABLE` with reason code `NOT_YET_IMPLEMENTED` and, in
machine formats, emits a consumable refusal document — unlike grammar errors,
which keep machine stdout empty.



| Code | Symbol | Meaning |
|---:|---|---|
| 0 | `SUCCESS` | Requested operation completed; no blocking review/error. |
| 10 | `REVIEW_REQUIRED` | Completed successfully with REVIEW items. |
| 11 | `PARTIAL_COMPLETION` | Some items completed; non-systemic item failures remain. |
| 12 | `NOTHING_TO_DO` | Valid request produced no actionable eligible files. |
| 20 | `INVALID_ARGUMENT` | CLI grammar/value error. |
| 21 | `INVALID_CONFIGURATION` | Config/schema/precedence failure. |
| 22 | `CAPABILITY_UNAVAILABLE` | Required codec/runtime/filesystem operation unavailable. |
| 23 | `INCOMPATIBLE_CONTRACT` | Job/model/schema/tool compatibility failure. |
| 30 | `STALE_PLAN` | Source or governing plan inputs changed. |
| 31 | `SAFETY_PREFLIGHT_FAILED` | Backup/space/alias/filesystem guard failed. |
| 32 | `VERIFICATION_FAILED` | Staged or committed output failed checks. |
| 33 | `RECOVERY_REQUIRED` | Unresolved journal state requires recovery/integrity action. |
| 34 | `CONFLICT` | Undo/destination/external modification conflict. |
| 40 | `CANCELLED` | Graceful cancellation with durable resumable state. |
| 50 | `INTERNAL_ERROR` | Unhandled defect/system invariant failure. |

The SPIKE-009 precondition for freezing was met and the freeze took effect at WP-002 entry (see above).

**SPIKE-009 evidence (2026-07-23,
[report](../../spikes/SPIKE-009-cli-contract/report.md)):** the registry and
machine-stream rules are validated on System.CommandLine 2.0.10 (DEP-002) with
golden tests and out-of-process byte-level checks; numerics may freeze at
WP-002 entry. Binding contract decisions recorded from the spike:

- Exit aggregation: failed > review > success > capability-unavailable >
  nothing-to-do; cancellation dominates all.
- Grammar errors keep machine stdout empty: diagnostics on stderr, exit 20;
  automation keys on the exit code.
- The host must reject option-like tokens bound into greedy positional
  arguments — the parser alone does not enforce the grammar guarantee.
- Machine output requires explicit UTF-8-without-BOM console encoding on
  Windows; redirected stdout otherwise uses the legacy codepage.
- Non-ASCII in machine output is `\uXXXX`-escaped; decoded values are the
  contract, and consumers must not byte-match non-ASCII paths.
- Response files: UTF-8, one argument per line, `#` comments, no nesting,
  expanded by the host before parsing.

## PowerShell behavior

- Document `$LASTEXITCODE`.
- Response files are UTF-8, one argument per line with escaping rules.
- Completion is generated from the same command model.
- Examples quote Windows paths correctly.
- JSON examples use `ConvertFrom-Json`; JSONL uses line streaming.
- No dependence on interactive confirmation in automation.
