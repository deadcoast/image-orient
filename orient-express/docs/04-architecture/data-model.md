---
document_id: DOC-DATAMODEL
title: Job, Event, and Persistence Data Model
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-ARCH, ADR-0003, ADR-0008]
traces_to: [PRD-FR-007, PRD-FR-008, PRD-FR-014, PRD-FR-028]
---

# Data model

## Job directory

```text
<job>/
  manifest.json                 # immutable identity/config summary after creation
  job.db                        # canonical transactional state
  plan.digest                   # seal over ordered plan representation
  reports/
    scan-summary.json
    apply-summary.json
    verify-summary.json
    decisions.csv               # optional export
  review/
    thumbnails/                 # opt-in, bounded, non-authoritative
  diagnostics/
    capabilities.json
    effective-config.redacted.json
  staging/                      # only when policy/location permits
  recovery/                     # durable recovery descriptors if external to DB
  logs/                         # bounded local logs
```

SQLite is canonical for work state; exported JSON/CSV is not silently re-imported except through explicit review/job import contracts.

## Core entities

### `jobs`

- `job_id` (ULID/UUID)
- schema and contract versions
- creation time and tool build
- mode, roots, config digest
- active model/runtime/provider/calibration/policy identities
- capability snapshot digest
- state and state version
- plan seal/digest
- retention/recovery flags

### `files`

- stable `file_id` within job
- canonical sequence number
- source display path and normalized path token
- handle identity where available
- size/timestamps/attributes
- weak and strong fingerprints
- format/image descriptor
- metadata snapshot digest
- discovery warnings

Sensitive absolute paths remain local; diagnostics exports can tokenize/redact them.

### `assessments`

- file ID and assessment version
- EXIF orientation/raw transform
- per-candidate face/evidence summaries
- feature vector schema/version
- calibrated probability vector
- selected decision, confidence, margin
- evidence sufficiency and reason codes
- model/runtime/provider/calibration/policy provenance

Large raw tensors and full images are not stored.

### `plans`

- file ID
- source snapshot reference
- effective decision (base plus optional review override)
- composed transform
- output mode/path token/collision policy
- JPEG edge/fallback policy
- metadata mutation/preservation profile
- expected dimensions/orientation
- verification contract
- estimated bytes/work
- plan-row digest and seal membership

### `review_decisions`

Append-only records:

- review decision ID
- file/plan-row digest
- chosen action or skip
- source (`interactive`, `csv-import`, `api-future`)
- actor label (optional local string)
- time, schema, comment
- validation result and supersedes reference

### `events`

Append-only ordered facts with job sequence and per-file sequence:

- event ID/version/type/time
- job/file/correlation IDs
- stage/state transition
- action/result/reason/error category
- durations and counters
- structured data
- tool/model/runtime provenance where relevant

### `journal`

- intent ID and operation type
- source, staged, backup, destination identities
- expected pre/post fingerprints
- checkpoint (`Prepared`, `StagedVerified`, `IntentDurable`, `Replaced`, `Committed`, `Reconciled`, `Undo...`)
- operating-system result details
- recovery decision and completion

### `schema_migrations`

Applied migration ID, from/to version, tool build, time, digest, and status.

## State-transition constraints

Database constraints/application transactions prevent:

- apply before plan seal;
- mutation of sealed assessment/plan rows;
- duplicate terminal commit for a row;
- commit without successful verification;
- undo without committed in-place operation and verified backup;
- pruning a job with unresolved journal intent;
- activating incompatible model/job schema silently.

## Transaction strategy

- Single logical writer through a bounded persistence queue.
- Batch discovery/assessment writes.
- Seal uses a stable ordered canonical serialization and digest.
- Journal intent transaction commits before filesystem replacement.
- Completion transaction commits after replacement verification.
- WAL mode is evaluated only on supported local filesystems; job store refuses unsafe network/WAL combinations or selects an approved rollback mode.
- Explicit checkpoints prevent unlimited WAL growth.
- `PRAGMA integrity_check`/application invariants back `jobs integrity`.

## Fingerprints

A fingerprint policy can include:

- handle-derived volume serial/file ID;
- size and selected timestamps;
- prefix/suffix digest for early detection;
- full SHA-256 or approved modern digest before destructive apply;
- metadata or pixel semantic digest where needed.

The job records which strength was used. A weaker fingerprint cannot satisfy an apply policy requiring a stronger one.

## Schema evolution

- Major contract changes require migration and compatibility policy.
- Migrations are forward-only in normal operation; backup is created before mutating a job DB.
- A newer unsupported major version is read-only diagnosed, never guessed.
- Job export/import is a separate signed/digest-checked contract, not raw DB copying as an API.
