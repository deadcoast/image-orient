# SPIKE-004 report — metadata round-trip and ExifTool integration

**Status:** complete for baseline formats — accepted under single-operator governance (2026-07-23); maker-note matrix capability-gated on the SPIKE-001 corpus
**Run:** 2026-07-23, Windows 11 (build 10.0.26200), .NET 10.0.8; ExifTool 13.59 (DEP-007, sha256 `ce389bf0…` recorded in results)
**Raw evidence:** [`results/spike004-results.json`](results/spike004-results.json) · **Harness:** [`README.md`](README.md)

## Question and exit gate

Can a persistent ExifTool adapter write orientation without damaging any
other metadata family, with restorable timestamps and a reliable error
channel? Gate: round-trip matrix and explicit guarantees/non-guarantees; no
unexplained critical-field loss in baseline formats.

## Results — 6/6 scenarios pass

- **Round-trip matrix (JPEG):** with 53 tags across EXIF (12), GPS (5), XMP
  (4), IPTC (3) plus file/system groups, an orientation write changed
  **exactly one tag** (`IFD0:Orientation`) — zero tags lost, zero tags
  altered, JPEG marker structure (SOF/SOS/APP1) stable. **PNG:** XMP
  round-trips and the container stays structurally valid.
- **Protocol:** one process served all commands with per-command
  synchronization; an invalid write surfaced `Tag 'NotARealTagName' is not
  defined` on the **synchronized stderr channel** (`-echo4` sentinel) and the
  same process kept serving correct writes afterwards.
- **Timestamps:** `-P` preserves the filesystem modify time exactly; without
  it the mtime changes. Timestamp restoration is therefore a flag-discipline
  guarantee, not a separate restore pass, for the common case.
- **Rewrite side effects:** ExifTool's `-overwrite_original` rewrite **drops
  alternate data streams and changes the NTFS file ID**. This is the same
  class of behavior SPIKE-006 measured for `ReplaceFileW`: identity must
  re-baseline after any metadata write, and ADS survival is not part of the
  metadata guarantee (DOC-FSSAFE's ADS handling belongs to the commit
  protocol, not the metadata adapter).

## Guarantees / non-guarantees (baseline formats)

| Guaranteed (evidence-backed) | Not guaranteed |
|---|---|
| Orientation write alters only `IFD0:Orientation` | Maker-note preservation (needs real camera corpus — SPIKE-001 tranche) |
| EXIF/GPS/XMP/IPTC families byte-stable through the write | ADS survival across a write (re-attach/verify is the executor's job) |
| Filesystem mtime preserved with `-P` | File-ID stability across a write (re-baseline required) |
| Errors attributable per command; process survives bad input | Sidecar (`.xmp`) semantics (not yet exercised) |

## Adapter design decisions carried into WP-005

1. Typed operations only; the allowlisted argfile protocol with
   `-execute<id>`/`{ready<id>}` **plus a per-command `-echo4` stderr
   sentinel** — without it, stderr diagnostics are not attributable (found
   and fixed during this spike).
2. Reads use `-j -G1 -a -n`; group-qualified names (`IFD0:`, `ExifIFD:`,
   `GPS:`) are the comparison vocabulary.
3. Writes use `-P -overwrite_original`; the metadata write happens on the
   **stage copy** in the commit protocol, so the ADS/file-id side effects
   fold into the already-required post-replace re-baseline.

## Limitations

- Synthetic fixtures: maker notes, thumbnails, and device-specific quirks
  await the consented corpus tranche (explicitly non-guaranteed until then).
- Single-file sequential use; `-stay_open` worker-pool behavior and
  large-batch throughput belong to WP-005/WP-021.
- Sidecar policy untested.

## Affected items

- **ADR-0006 0.1.1** — provisional → accepted (baseline-format gate met).
- **DEP-007** — selected; 13.59 digest-pinned; re-run matrix on every bump.
- **DOC-WBS** — design notes for WP-005/WP-010/WP-014.
- **planning/spike-register.csv** — SPIKE-004 → complete (maker notes gated).

## Spike completion checklist (DOC-SPIKES)

- [x] Falsifiable question and thresholds declared before the run.
- [x] Inputs/fixtures documented — synthetic; no consent burden.
- [x] Harness repeatable from a clean checkout (tool paths parameterized).
- [x] Raw output machine-readable with tool version and digest.
- [x] Failure cases included (invalid tag, stderr attribution, ADS/file-id side effects).
- [x] Recommendation compares alternatives (WIC metadata writer remains the recorded fallback in DEP-007).
- [x] ADR and dependency register updated — ADR-0006 0.1.1; DEP-007.
- [x] Follow-on work package plans updated — DOC-WBS notes for WP-005/010/014.
- [x] Artifact retained in-repo under the evidence policy.
