# WP-013/WP-014 context packet — verification and the safe-copy end-to-end workflow

Generated 2026-07-24 per DOC-DEVPLAN ("Work-package context packet").
Covers WP-013 (independent verification) and WP-014 (safe-copy end-to-end
mode) — delivered together because apply without verification would violate
PRD-FR-013 by construction.

## What works now (first working product path)

`upright scan <source> --output <dir>` then `upright apply <job>`:

1. **scan** (read-only, AC-014): enumerates JPEGs in canonical order,
   captures handle identity + fingerprints, probes format/dimensions/EXIF
   orientation with the bounded managed prober, plans deterministically —
   EXIF tag 2..8 proves a rotation (Normalize via the WP-010 planner);
   tag 1/absent is Keep; everything else is a recorded Unsupported row —
   and seals the immutable plan (WP-009 store) with reports and events.
2. **apply** (safe-copy only): per Normalize row — refuse stale inputs by
   recorded identity (AC-015), lossless jpegtran transform into an
   exclusive stage file beside the destination, orientation tag set to 1
   on the stage copy, **independent verification** (WP-013), then the
   atomic create-path move. Sources are never written. Failed rows are
   recorded events; the run continues; `--resume` skips rows whose commit
   event exists (AC-028). Exit codes: 0 / 11 partial / 12 nothing /
   30 stale.

## WP-013 delivered

- `OrientExpress.Contracts.Verify`: `VerificationContract` (plan-authored
  expectations), stable failure codes, `VerificationResult`.
- `OrientExpress.Application.Verify`: `IOutputVerifier` port.
- `OrientExpress.Infrastructure.Imaging`: `ProbeOutputVerifier` — reopens
  the staged output with the managed header prober (independent of
  jpegtran/ExifTool, which produced the file) and rejects missing,
  truncated, non-image, wrong-format, wrong-dimension, wrong-orientation
  outputs, collecting every failure (AC-025). A failed verification never
  commits and never touches the source (AC-026).

## Honest scope

- Deterministic path only: photos stored sideways with a clean tag are
  planned Keep — the ML decision engine stays gated on corpus evidence
  (WP-007/008). Scan output says this explicitly.
- Safe-copy only; in-place refuses (WP-012/WP-015 journal + undo).
- Failed staged outputs are deleted, not quarantined-preserved;
  quarantine-preserve and the standalone `upright verify` re-check land
  with WP-017/WP-019.
- CLI options are the working subset (`--output`, `--no-recurse`,
  `--fingerprint`, `--max-files`, `--resume`); include/exclude globs,
  collision modes beyond error, `--jpeg-edge` per-run override, and
  Ctrl+C double-interrupt wiring are additive follow-ups.
- Keep rows are not copied to the output tree (outputs contain only
  corrected photos).
- End-to-end proof: 3 CLI tests build real tagged JPEGs, scan, apply,
  verify rotated dimensions + tag 1 via the independent prober, prove
  sources untouched, resume idempotence, stale refusal, and collision
  refusal.
