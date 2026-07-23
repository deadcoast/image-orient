# SPIKE-009 report — CLI contract and automation ergonomics

**Status:** complete — accepted under single-operator governance (2026-07-23)
**Run:** 2026-07-23, Windows 11 (build 10.0.26200), .NET 10.0.8, System.CommandLine 2.0.10 (DEP-002)
**Raw evidence:** [`results/spike009-results.json`](results/spike009-results.json) · **Harness:** [`README.md`](README.md)

## Question and exit gate

Can the DOC-CLI contract — mixed human/machine output, exit-code registry,
response files, completion, cancellation, and destructive confirmation — be
implemented stably and safely on the selected parser stack?

Gate (DOC-SPIKES): CLI UX contract approved and versioned; all destructive
cases require explicit flags; machine streams are parse-clean.

## Results

All 11 out-of-process evidence scenarios pass and 20 in-process golden tests
run in CI. Measured against the gate:

- **Machine streams are parse-clean.** JSONL stdout is BOM-free strict UTF-8;
  every line independently parses and carries the five required
  `event.schema.json` fields; JSON mode emits exactly one document declaring
  `resultType: command.result`; progress and diagnostics go to stderr only,
  and `--non-interactive` suppresses progress entirely.
- **Destructive cases require explicit flags.** An in-place plan refuses to
  apply without `--acknowledge-in-place`, failing before any work with
  `INVALID_ARGUMENT` and reason code `IN_PLACE_NOT_ACKNOWLEDGED`. A tampered
  sealed plan is refused with `30 STALE_PLAN` via row-digest re-verification —
  the scan-seals/apply-consumes contract (ADR-0003) holds end to end.
- **Contract is versioned.** `cli-contract/1.0-draft` is embedded in every
  machine document; the exit registry (0/10/11/12/20/22/30/40 exercised) is
  validated with a deterministic aggregation rule: failed > review > success >
  capability-unavailable > nothing-to-do.
- **Automation ergonomics work.** `@file` response files (UTF-8, one argument
  per line, `#` comments, no nesting) expand to identical results; a missing
  response file is a grammar error. PowerShell completion is generated from
  the same command model and is byte-deterministic. Simulated cancellation
  exits `40 CANCELLED` with a terminal `cancelled` event. `--correlation-id`
  is echoed on every event.

## Findings that change downstream work (WP-002 inputs)

1. **Greedy positionals swallow unknown options.** System.CommandLine binds
   unknown `--tokens` into a `string[]` OneOrMore argument instead of
   erroring. The production host must validate option-like path tokens
   explicitly (or constrain grammar) to keep the DOC-CLI grammar guarantee.
2. **Windows redirected stdout defaults to the legacy codepage.** Machine
   output must set the console encoding to UTF-8-without-BOM explicitly
   (`Console.OutputEncoding`), or JSONL consumers on Windows receive
   ANSI/OEM-mangled bytes.
3. **Non-ASCII is escaped on the wire.** System.Text.Json emits `\uXXXX`
   escapes; decoded values round-trip exactly. Consumers must not
   substring-match raw bytes for non-ASCII paths — document this in the
   machine-contract notes.
4. **Response files:** explicit host-side expansion (one argument per line)
   is simple, deterministic, and testable; the contract should specify it
   rather than relying on parser internals.
5. **Grammar errors keep machine stdout empty.** Parse failures report on
   stderr with exit 20 and emit no machine document; automation should key on
   the exit code. Recorded as the contract decision.

## Limitations

- Human-usability evidence is limited to contract-shape checks (first-line
  rule, no ANSI when redirected); real user feedback accumulates from the M1
  diagnostic alpha onward. Numeric registry values freeze at WP-002 entry per
  DOC-CLI; symbols are stable now.
- Real Ctrl+C delivery (`GenerateConsoleCtrlEvent`) was not exercised;
  cancellation was simulated at the loop boundary. WP-002 must test genuine
  console signals.
- `review`, `jobs`, `models`, and `config` subtrees were not prototyped; they
  add no new contract mechanics.

## Recommendation

Adopt System.CommandLine 2.0.10 (DEP-002 confirmed) with the host-side
guards above. Freeze the exit-code registry numerics at WP-002 entry.
Alternatives considered: hand-rolled parser (rejected: completion/help/
response-file surface for free and DEP-002 isolates the contract behind
golden tests); Spectre.Console.Cli (rejected for the contract layer: renderer
concerns are separable and DEP-003 stays a candidate for human output only).

## Affected items

- **DOC-CLI 0.1.1** — records the evidence and the five contract decisions above.
- **DEP-002** — qualification evidence recorded; version pinned centrally.
- **WP-002, WP-018** — unblocked (SPIKE-009 was their gating spike) with the
  findings above as design inputs.
- **planning/spike-register.csv** — SPIKE-009 → complete.

## Spike completion checklist (DOC-SPIKES)

- [x] Falsifiable question and pass/fail threshold stated before experimentation.
- [x] Inputs/fixtures documented — synthetic path tokens, no image data.
- [x] Harness repeatable from a clean checkout (`README.md`).
- [x] Raw output machine-readable (`results/spike009-results.json`).
- [x] Failure cases included (tampered plan, missing response file, unknown options, conflicting formats, unsupported shell).
- [x] Recommendation compares credible alternatives.
- [x] ADR and dependency register updated — DEP-002 evidence; ADR-0010 versioning honored (`cli-contract/1.0-draft`).
- [x] Follow-on work package plans updated — WP-002 design inputs recorded in DOC-CLI 0.1.1.
- [x] Artifact retained in-repo under the evidence policy (small, non-private).
