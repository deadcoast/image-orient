# SPIKE-007 report — format and codec capability matrix

**Status:** complete — accepted under single-operator governance (2026-07-23)
**Run:** 2026-07-23, Windows 11 (build 10.0.26200), .NET 10.0.8; WIC (OS-provided, DEP-009)
**Raw evidence:** [`results/spike007-results.json`](results/spike007-results.json) · **Harness:** [`README.md`](README.md)

## Question and exit gate

Which formats can be guaranteed on the supported platform, how is optional
codec support detected truthfully per machine, and what does that mean for
distribution? Gate: baseline format matrix approved; JPEG/PNG guaranteed;
optional formats explicitly gated with tested diagnostics.

## Results — all safety expectations pass

**Baseline matrix (guaranteed on supported Windows 11):**

| Format | Decode | Encode | Round-trip | Metadata read |
|---|---|---|---|---|
| JPEG | ✔ | ✔ | dimension-exact | ✔ EXIF orientation via WIC query (`System.Photo.Orientation` = 6 from an ExifTool-authored file) |
| PNG | ✔ | ✔ | dimension-exact | (XMP via ExifTool adapter) |
| TIFF/BMP/GIF | ✔ | ✔ | dimension-exact | — |

**Optional formats on this machine (per-machine truth, not a product claim):**
HEIC **present** (engaged), AVIF **present** (engaged), WebP **present**
(engaged), JPEG XL **absent** (`CODEC_NOT_INSTALLED`). Presence means the
codec engaged a truncated header; full decode proof still requires real
samples from the corpus tranche — reason code `CODEC_PRESENT_UNVERIFIED`
until then.

## Method findings (binding on WP-005's capability probe)

1. **Header-engagement probing is the capability mechanism.** Feed the
   decoder a truncated real header: component-not-found (wrapped
   `NotSupportedException` / `0x88982F50`) ⇒ absent; format/data rejection ⇒
   present and engaged. No sample corpus needed, no false claims.
2. **Do not enumerate.** The registry `Instance` categories list only add-on
   codecs (this machine: Raw + JPEG XL... which then *failed* engagement —
   registry presence ≠ operational capability), and managed `ComImport`
   enumeration of WIC components returned empty. Enumeration lies twice;
   operation-level probes tell the truth.
3. HEIC availability is a two-part dependency (HEIF container + HEVC codec);
   engagement probing captures the net effect per machine.

## Licensing / distribution inventory (WP-022 input)

- **WIC**: OS component, no redistribution — baseline path carries zero
  codec licensing burden.
- **HEIF/HEVC/AV1 extensions**: user-installed Store components; never
  bundled; capability-gated per machine (matches NFR-COMP-001 discipline).
- **Magick.NET (DEP-010)**: remains a *candidate* fallback only; Apache-2.0
  core but delegate licensing review required before any adoption; not
  empirically compared here (recorded limitation) — the WIC baseline meets
  the gate without it.
- **libjpeg-turbo (DEP-008)**: already qualified for transforms (SPIKE-005);
  BSD notices at packaging.

## Limitations

- Optional-format decode proof (real HEIC/AVIF samples) awaits the corpus
  tranche; until then they stay `CODEC_PRESENT_UNVERIFIED`.
- Magick.NET comparison deferred (candidate stays candidate).
- WPF/WIC wrappers probed; a raw-WIC production adapter would re-verify via
  the same engagement probes.

## Affected items

- **DEP-009** — WIC selected as the baseline codec/probe path.
- **DEP-010** — stays candidate; delegate-license review precondition recorded.
- **DOC-WBS** — design note for WP-005/WP-010/WP-022.
- **planning/spike-register.csv** — SPIKE-007 → complete.

## Spike completion checklist (DOC-SPIKES)

- [x] Falsifiable question and thresholds declared before the run.
- [x] Inputs/fixtures documented — synthetic; no user photos.
- [x] Harness repeatable from a clean checkout.
- [x] Raw output machine-readable.
- [x] Failure cases included (absent codec, truncated-sample rejection, enumeration unreliability).
- [x] Recommendation compares alternatives (WIC vs Magick.NET with licensing rationale).
- [x] ADR and dependency register updated — DEP-009/DEP-010 (ADR-0001 unchanged: WIC was already the primary candidate).
- [x] Follow-on work package plans updated — DOC-WBS note.
- [x] Artifact retained in-repo under the evidence policy.
