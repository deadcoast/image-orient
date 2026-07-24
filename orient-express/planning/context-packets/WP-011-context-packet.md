# WP-011 context packet — bounded execution pipeline

Generated 2026-07-24 per DOC-DEVPLAN ("Work-package context packet").

## Objective

The engine that runs scan/apply stages without unbounded memory or silent
stops (PRD-FR-022, PRD-FR-014): ordered bounded stages with backpressure,
per-stage worker limits from DOC-CONFIG `performance.*`, typed-error retry,
per-item failure isolation, and two-token cancellation.

## Delivered

- `OrientExpress.Contracts.Pipeline`: `IProgressSink` (ephemeral UI;
  events remain the durable facts), `PipelineProgress`, `StageLimits`
  (sanitized from config), `RetryPolicy`.
- `OrientExpress.Application.Pipeline`: `BoundedPipeline` on BCL
  `System.Threading.Channels` (Application stays package-free) —
  `PipelineStage<TItem>` list with per-stage bounded queues and worker
  counts; items stream to a terminal consumer as they finish (no unbounded
  result accumulation); a failing item is recorded with its stage and typed
  error and leaves the pipeline (one bad file never stops the run); retry
  only when the typed error says `IsRetryable` (bounded attempts,
  exponential backoff); soft cancellation stops intake and finishes
  admitted items (resumable state, AC-042 mechanism), hard abort abandons
  promptly.

## Acceptance criteria addressed

- AC-041 (mechanism): worker/queue limits provably bound stage concurrency
  and in-flight items in tests; the 100,000-file synthetic memory
  measurement runs when scan is wired end to end (WP-014).
- AC-042 (mechanism): soft/hard token semantics proven in tests; the
  Ctrl+C double-interrupt wiring to those tokens is CLI work (WP-014).

## Honest scope

- `scan`/`apply` still refuse: this is the engine, not the commands. The
  scan planner and apply executor that put stages INTO this pipeline are
  WP-013/WP-014.
- Resource governors beyond worker/queue bounds (memory ceilings,
  `performance.memoryMiB`) are enforced when real stages exist to govern
  (WP-014); the limit plumbing is in place.
