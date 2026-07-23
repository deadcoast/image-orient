# SPIKE-006 report — crash-safe atomic replace on Windows (NTFS pilot)

**Status:** complete — accepted under single-operator governance (2026-07-23); non-NTFS classes stay capability-gated
**Run:** 2026-07-23, Windows 11 (build 10.0.26200), .NET 10.0.8, NTFS, single fixed volume
**Raw evidence:** [`results/spike006-results.json`](results/spike006-results.json) · **Harness:** [`README.md`](README.md)

## Question and exit gate

Can the DOC-FSSAFE in-place protocol (verified backup → same-directory stage →
flush → durable journal intent → `ReplaceFileW` → independent verify → durable
commit) recover from a crash at **every** step boundary on NTFS with **zero
source-loss cases**, and what does `ReplaceFileW` actually preserve?

Gate (DOC-SPIKES): recovery state machine proven on NTFS; unsupported
filesystems/conditions capability-gated; fault matrix has no source-loss case.

## Results

**Fault matrix: 12/12 cases, zero source loss.** A child process was
kernel-terminated (`TerminateProcess`, no flushes or finalizers) at each of the
eight protocol boundaries, plus a no-fault baseline and three adversarial
variants (stage corrupted after durable intent; source tampered externally
after intent; source tampered after commit). In every case the observed
recovery action matched the DOC-FSSAFE recovery matrix, the original bytes
remained recoverable from the source or the verified backup, and a second
recovery pass was a non-mutating no-op (idempotency). The recovery state
machine is additionally proven row-by-row and for totality by 31 unit tests
that run in CI.

**Primitive qualification (measured on NTFS):**

| Property across `ReplaceFileW` | Preserved? | Notes |
|---|---|---|
| Replaced file's creation time | yes | last-write time comes from the replacement |
| Hidden attribute | yes | |
| Alternate data stream (named) | yes | payload intact |
| DACL incl. explicit ACE | yes | SDDL byte-identical (compared as digests) |
| **NTFS file ID** | **no** | result carries the *replacement's* file ID |

| Boundary case | Result | Win32 error |
|---|---|---|
| Target open without delete sharing | rejected, target and replacement intact | 32 `ERROR_SHARING_VIOLATION` |
| Target open with read/write/delete sharing | replace succeeds | — |
| Read-only target | rejected, unchanged | 5 `ERROR_ACCESS_DENIED` |
| Missing target | rejected; `MoveFileEx(REPLACE_EXISTING\|WRITE_THROUGH)` is the qualified create-path fallback | 2 `ERROR_FILE_NOT_FOUND` |
| Cross-volume replacement | **not exercised** (single fixed volume) | stays capability-gated |
| `lpBackupFileName` | receives the original content verbatim | — |
| Directory-handle `FlushFileBuffers` | works on NTFS | — |

**`MoveFileEx(MOVEFILE_REPLACE_EXISTING)` comparison:** content correct, but
creation time, alternate data streams, and file ID are all lost. It is not an
acceptable in-place primitive; its only qualified role is creating a
destination that does not exist yet (safe-copy path).

**Journal:** a torn trailing journal line (crash mid-append) is ignored and
the previous durable checkpoint governs, by construction and by test.

## Limitations and failed cases

- NTFS on one fixed volume only. ReFS, SMB, FAT/exFAT, and the cross-volume
  rejection path are unmeasured and must stay capability-gated.
- Crash simulation is process-level kernel termination, not OS crash or power
  loss; write-through and explicit flushes bound but do not prove
  power-failure behavior.
- No antivirus/indexer interference forcing, no cloud placeholders, reparse
  points, or hardlink aliases were exercised.
- The undo rows of the DOC-FSSAFE recovery matrix are not yet implemented in
  the harness.
- Single-file jobs only; concurrency and journal scale belong to SPIKE-008.

## Recommendation (pending review)

1. Adopt `ReplaceFileW` with `REPLACEFILE_WRITE_THROUGH` as the sole in-place
   commit primitive, exactly as staged in DOC-FSSAFE. Alternatives rejected:
   `MoveFileEx` replace (loses creation time/ADS/identity, above); TxF
   (deprecated by Microsoft); plain rename-over (no preserved-property or
   backup semantics).
2. Treat sharing-violation (32) and access-denied (5) as actionable
   stop/retry classes; require an explicit precondition or policy for
   read-only sources before in-place is offered.
3. **Design consequence for WP-004:** the NTFS file ID does not survive
   `ReplaceFileW`. Handle-based identity is valid for pre-commit alias/lock
   checks, but plans and journals must re-baseline identity after commit and
   must not use the file ID as a cross-commit key. DOC-FSSAFE's identity
   section should record this explicitly at its next reviewed revision.
4. Keep in-place mode blocked (STATUS.md) until the remaining gate evidence
   exists: multi-volume and non-NTFS capability classes, AV interference, the
   undo path, and cross-domain review sign-off.

## Affected items

- **ADR-0009** — evidence supports the decision; proof gate remains open
  pending the limitations above.
- **DOC-FSSAFE** — add the file-ID re-baseline caveat (reviewed change).
- **WP-004, WP-011, WP-012, WP-015** — design inputs delivered (identity
  re-baseline, error-class handling, recovery executor shape).
- **RISK-005** — mitigation evidence recorded; risk stays open until the full
  compatibility matrix exists.
- **planning/spike-register.csv** — SPIKE-006 status updated.

## Spike completion checklist (DOC-SPIKES)

- [x] Falsifiable question and pass/fail threshold stated before experimentation.
- [x] Inputs/fixtures documented — synthetic, deterministic, no consent burden.
- [x] Harness repeatable from a clean checkout (`README.md`).
- [x] Raw output machine-readable (`results/spike006-results.json`).
- [x] Failure cases and security-relevant behavior included (tamper, corrupt, locked, read-only).
- [x] Recommendation compares credible alternatives.
- [x] ADR and dependency register updated — ADR-0009 0.1.1 records the evidence; DOC-FSSAFE 0.1.1 records the file-ID caveat; no dependency changes.
- [x] Follow-on work package plans updated — DOC-WBS 0.1.1 "Spike-driven design notes" for WP-004/011/012/015.
- [x] Artifact retained in-repo under the evidence policy (small, non-private).
