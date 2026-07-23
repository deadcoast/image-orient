---
document_id: DOC-FSSAFE
title: Filesystem Safety, Atomicity, Recovery, and Undo
version: 0.1.1
status: baseline
as_of: 2026-07-23
owner: Core
depends_on: [DOC-ARCH, ADR-0003, ADR-0009]
traces_to: [PRD-FR-009, PRD-FR-010, PRD-FR-013, PRD-FR-014, PRD-FR-015, NFR-REL-001, NFR-REL-002, NFR-REL-003, NFR-REL-004]
---

# Filesystem safety

## Threat model

Photos are irreplaceable user data. Correctness must hold under process crash, reboot, disk-full, permission changes, sharing violations, antivirus scanning, cloud hydration, file replacement by another process, duplicate paths, hardlinks, reparse points, unsupported filesystem semantics, and malformed files.

## Safe-copy default

For each output:

1. Resolve a destination under the configured output root.
2. Confirm it cannot alias the source or another planned destination.
3. Apply deterministic collision policy.
4. Stage in the destination directory or same volume.
5. Write/transform, apply metadata, flush, and close.
6. Reopen independently and verify.
7. Atomically move/rename into the final destination when supported.
8. Verify the final path identity/state.
9. Record completion.
10. Leave source untouched.

Existing unrelated destinations are never overwritten by default.

## In-place protocol

In-place is allowed only when all preconditions pass:

- explicit `--acknowledge-in-place`;
- supported local filesystem/capability class;
- source identity and strong fingerprint match the plan;
- source is not an unhandled reparse/placeholder/link case;
- verified backup target has capacity and cannot alias source/stage;
- backup is copied, flushed, reopened, and digest-verified;
- same-volume source-directory staging is possible;
- metadata/JPEG/output verification contract is available;
- durable job store and journal are healthy.

Protocol:

```text
capture current source
 -> create + verify backup
 -> create same-directory unique stage
 -> transform + metadata
 -> flush file and relevant directory/journal boundaries
 -> independent verify stage
 -> commit journal IntentDurable
 -> ReplaceFileW or qualified equivalent
 -> reopen final path and verify
 -> commit journal Committed
 -> governed cleanup
```

## Recovery matrix

| Durable journal checkpoint | Observed filesystem | Recovery action |
|---|---|---|
| Before `IntentDurable` | source original; stage may exist | Remove/quarantine uncommitted stage after verification; source remains authoritative |
| `IntentDurable`, source original | stage verified exists | Retry qualified replacement or stop with actionable lock/capability error |
| `IntentDurable`, source matches expected output | backup verified | Record recovered commit after independent verification |
| `IntentDurable`, neither expected source nor output | any | Stop as critical conflict; preserve all artifacts |
| `Committed`, source matches expected output | any | Idempotent success; finalize checkpoint |
| `Committed`, source changed externally | backup exists | Report drift; never overwrite automatically |
| Undo intent, source expected output | backup verified | Complete restore |
| Undo intent, source independently changed | backup verified | `UNDO_CONFLICT`; preserve both |

## Atomic primitive qualification

`ReplaceFileW` is the primary Windows candidate because it is designed to replace file contents while retaining selected properties of the replaced file. The spike must test actual behavior for:

- ACL/security descriptors;
- timestamps and attributes;
- alternate data streams;
- encryption/compression;
- open handles and sharing flags;
- antivirus/indexer interference;
- cross-volume rejection;
- failures and error codes;
- NTFS/ReFS/SMB differences.

No API name is treated as a guarantee without the fault matrix.

**Measured (SPIKE-006 NTFS pilot, 2026-07-23):** `ReplaceFileW` preserves the
replaced file's creation time, attributes, alternate data streams, and DACL,
and takes last-write time from the replacement — but it does **not** preserve
the NTFS file ID; the result carries the replacement's identity. `MoveFileEx`
with `MOVEFILE_REPLACE_EXISTING` additionally loses creation time and ADS and
is therefore qualified only for creating a destination that does not yet
exist (safe-copy path), never for in-place replacement. Sharing-violation
(32) and access-denied (5) rejections leave both files intact and are
actionable stop/retry classes. Cross-volume rejection, ReFS, and SMB remain
unmeasured and capability-gated. Evidence:
[SPIKE-006 report](../../spikes/SPIKE-006-atomic-replace/report.md).

## Identity and alias handling

- Open handles with sharing flags appropriate to read/replace.
- Query volume serial and file ID where supported.
- File IDs are valid for pre-commit alias/lock checks only: the NTFS file ID
  does not survive `ReplaceFileW` (SPIKE-006), so plans and journals must
  re-baseline identity after commit and must never use the file ID as a
  cross-commit key.
- Detect source=destination after normalization and handle identity.
- Deduplicate hardlink aliases during planning.
- Default: do not traverse reparse points.
- Default: do not mutate offline cloud placeholders; optionally hydrate explicitly in a future policy.
- Long paths use Unicode APIs/manifest and normalized internal representation without trimming meaningful characters.
- Case-insensitive collision behavior follows the target filesystem, not ordinal string assumptions.

## Disk-space model

Plan estimates backup, stage, destination, job, and safety margin. Apply rechecks available space. Sparse/compressed/encrypted behavior is reported but not assumed. A disk-full error never triggers deletion of the source or verified backup.

## Temporary files

Names are unpredictable and created with exclusive semantics. Stage files remain on the same volume as commit target. They are not placed in a shared global temp directory for in-place commits. Orphan cleanup checks journal ownership and age; it never deletes an unrecognized file by naming convention alone.

## Timestamp and metadata policy

Filesystem creation/write/access timestamps and attributes are separate from embedded metadata. The preservation policy names which are restored. Timestamp restoration occurs before final verification where possible; failures are warnings or blockers according to policy.

## Backup retention

Backups are content-addressed or uniquely job-scoped, carry a manifest/digest, and cannot be pruned while an unresolved journal or undo window exists. Prune is dry-run by default and reports reclaimable bytes.

## Safety proof

In-place mode cannot ship until SPIKE-006 and the full fault-injection suite demonstrate zero source-loss cases across every injected boundary and supported compatibility class.
