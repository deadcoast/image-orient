---
document_id: DOC-PIPELINE
title: Processing Pipeline and State Machines
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-ARCH, DOC-DATAMODEL, DOC-FSSAFE]
traces_to: [PRD-FR-007, PRD-FR-008, PRD-FR-013, PRD-FR-014, PRD-FR-022]
---

# Processing pipeline

## Phase A — scan/analyze (strictly read-only)

```text
Resolve configuration/capabilities
  -> discover canonical inputs
  -> capture identity/fingerprint
  -> probe format/dimensions
  -> read metadata
  -> derive EXIF display transform
  -> bounded decode
  -> generate four displayed candidates
  -> run face model
  -> extract/aggregate evidence
  -> calibrate and apply policy
  -> resolve output/transform/verification plan
  -> persist file plan and events
  -> seal plan
```

### Fast paths

- Unsupported extension or inaccessible input: terminal planned status without decode.
- Valid metadata plus policy that permits metadata-only normalization: visual inference may be skipped only if release evidence approves that policy.
- Already normalized images still receive enough analysis to avoid false assumptions when visual inference is enabled.
- Content digest work may be staged: cheap identity during discovery, strong digest before seal/apply according to policy.

## Phase B — review

REVIEW rows can be listed, inspected, or exported. Imported decisions are schema-validated, refer to existing plan row IDs/digests, use a finite action enum, and become append-only override events. They never edit raw model evidence.

## Phase C — apply

```text
Acquire job mutator lock
  -> validate job/schema/tool compatibility
  -> recover incomplete intents
  -> stream planned actions
  -> re-capture source identity/fingerprint
  -> verify destination/backup capacity and capabilities
  -> create same-volume staging target
  -> transform pixels/coefficients
  -> apply metadata mutation
  -> flush staged content
  -> independently reopen and verify
  -> write durable commit intent
  -> safe-copy move OR in-place atomic replacement
  -> verify committed path
  -> write durable completion event/checkpoint
  -> cleanup governed temporary state
```

A KEEP/REVIEW/UNSUPPORTED row emits a terminal apply event without mutation.

## Phase D — verify

`verify` can independently re-evaluate committed outputs against the job's verification contracts and report drift, missing files, metadata changes, wrong orientation, or digest mismatch. It does not silently repair.

## Phase E — undo

For in-place operations only:

1. reconcile incomplete journal state;
2. verify backup digest and required metadata;
3. verify current file matches expected post-apply state;
4. stage/flush restore artifact;
5. record undo intent;
6. atomically restore;
7. reopen and verify restored state;
8. record completion.

A changed current file creates `UNDO_CONFLICT`.

## Work-item states

| State | Meaning | Durable? |
|---|---|---|
| `Discovered` | Canonical input emitted | batched |
| `Inspected` | File/metadata/image descriptor captured | yes |
| `Assessed` | Four-candidate evidence and decision exist | yes |
| `Planned` | Destination, transform, policy, expected state resolved | yes |
| `PlanSealed` | Immutable plan digest covers row | yes |
| `Preflighted` | Apply-time source/capabilities match | yes |
| `Staged` | Transform and metadata output written | yes via intent/event |
| `Verified` | Staged artifact passes independent checks | yes |
| `CommitIntent` | Recovery knows expected replacement | yes, flushed |
| `Committed` | Destination/source path holds verified output | yes |
| `UndoIntent` / `Undone` | Restore in progress/completed | yes |
| `Review` / `Unsupported` / `Conflict` / `Failed` | terminal or operator-required status | yes |

## Bounded pipeline design

Use `System.Threading.Channels` or equivalent bounded queues. Defaults are calculated from logical processors, memory limit, provider concurrency, and storage class, but are visible in effective config. Independent limits:

- max discovered items buffered;
- max decoded pixels/bytes in flight;
- metadata worker count;
- inference session count;
- transform worker count;
- verifier count;
- SQLite batch size/latency;
- per-file and per-tool timeout.

An item holds decoded pixels only through feature extraction. Full-resolution output transforms stream or use native coefficient paths.

## Retry taxonomy

Retry only errors classified transient: sharing violation, short-lived antivirus lock, temporary cloud hydration, provider initialization race, or configured I/O retry class. Parser corruption, digest mismatch, unsupported format, stale source, verification mismatch, and policy refusal are non-retryable. Retry schedules are bounded, jittered, cancelable, and evented.

## Cancellation

First Ctrl+C requests graceful cancellation: stop discovery, drain/finish only atomic sections, checkpoint, and exit with resumable status. A second interrupt requests immediate termination. No cancellation is honored between a durable commit intent and the minimum reconciliation checkpoint unless the operating system terminates the process; recovery handles that case.

## Backpressure and fairness

The scheduler prevents large/slow files from starving small files and separates scarce provider/native-tool tokens. It does not reorder final plan output nondeterministically: canonical sequence IDs define report ordering even when execution is parallel.

## Failure containment

Per-file failure does not abort the job unless policy says fail-fast or a systemic invariant fails (job corruption, full destination, invalid active model, journal failure). Systemic failures stop new mutation and move to recovery-required state.
