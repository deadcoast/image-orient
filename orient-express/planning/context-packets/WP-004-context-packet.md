# WP-004 context packet — filesystem and identity foundation

Generated 2026-07-23 per DOC-DEVPLAN ("Work-package context packet").

## Objective

Implement COMP-FILESYSTEM/COMP-NATIVE's diagnostic-mode foundation: path
canonicalization with long-path semantics (NFR-COMP-002), handle-based file
identity and snapshots (`IFileIdentityService` per DOC-COMPONENTS), standard
and strong fingerprints (DOC-DATAMODEL), reparse/cloud-placeholder detection,
volume capability classification (NFR-COMP-001), same-directory stage
planning, and the SPIKE-006-qualified atomic primitives — surfaced through
`upright doctor --probe-target <dir>` (PRD-FR-002: "filesystem operation
probes when explicit target supplied").

## Mapped requirements and acceptance

- **PRD-FR-001** (identity portion: aliases/reparse/duplicate identities
  feed AC-002; full discovery is COMP-DISCOVERY at scan time).
- **PRD-FR-010** (foundation portion: same-volume staging + atomic
  replacement primitives; the guarded executor itself is WP-012/WP-015).
- **NFR-COMP-001**: NTFS on fixed volumes = supported; ReFS, network,
  removable, and unknown filesystems = capability-gated classes.
- **NFR-COMP-002**: Unicode + long paths via extended-length (`\\?\`) forms;
  no lossy normalization.

## Upstream spike conclusions (binding, SPIKE-006)

`ReplaceFileW(REPLACEFILE_WRITE_THROUGH)` is the sole in-place primitive;
`MoveFileEx(REPLACE_EXISTING|WRITE_THROUGH)` only for create-path; the NTFS
file ID does **not** survive replacement — file IDs are pre-commit identity
only and snapshots re-baseline after commit; sharing-violation (32) and
access-denied (5) are actionable stop/retry classes; stage files are
same-directory, unpredictable, exclusively created.

## Design constraints (DOC-ARCH layering)

- `OrientExpress.Contracts`: `Result<T>`/`ErrorInfo` common model
  (DOC-COMPONENTS) and filesystem records (`NormalizedPath`,
  `FileFingerprint`, `FileSnapshot`, `SnapshotComparison`,
  `FilesystemCapability`). Package-free.
- `OrientExpress.Application`: ports (`IFileIdentityService`,
  `IAtomicFileOperations`, `IVolumeCapabilityProbe`) plus pure
  `PathCanonicalizer` and `StagePathPlanner`. No I/O.
- `OrientExpress.Infrastructure.Windows`: Win32 adapters (handle info,
  volume info, attributes, digests, ReplaceFileW/MoveFileEx). References
  inner layers only (architecture tests enforce).
- `OrientExpress.Cli`: composition + doctor probe wiring.

## Threat/risk notes

RISK-005 mitigations carried from SPIKE-006. Probe writes happen only under
an explicitly supplied `--probe-target` directory using unpredictable
exclusive-create names, and are always cleaned up; no image files are ever
touched. Reparse points and cloud placeholders are reported, never
traversed or hydrated (DOC-FSSAFE defaults).

## Test and evidence plan

`tests/OrientExpress.InfrastructureTests` (real NTFS temp dirs): identity
stability across reopens and distinctness across files and hardlink alias
detection; drift detection (content/size/time changes); standard vs strong
fingerprints including a content change that preserves size+timestamp
(prefix digest catches it); long-path (>260 chars) canonicalization and
round-trip; reparse detection (skips when symlink creation is not
permitted); atomic replace + create-path fallback; stage-path planning;
capability probe on NTFS. CLI golden tests: `doctor --probe-target`
document shape and cleanup; refusal on a nonexistent target.

## Dependencies and consumers

No new packages. Consumers: COMP-DISCOVERY (WP-005+ scan), WP-009 snapshots
in plans, WP-011/WP-012 executor/journal, doctor.

## Out of scope

Discovery enumeration/filtering (scan WPs); journal + recovery execution
(WP-012); backup planning (WP-012/015); hydration policy; SMB/ReFS behavior
(capability-gated, untested by design per NFR-COMP-001).

## Definition of done

Validator/build/format/tests green; probes clean up after themselves;
capability truth-telling in doctor updated; statuses, STATUS, CHANGELOG,
manifest updated; traceability intact.
