# SPIKE-001 harness — corpus inventory and truth protocol

Tooling for [SPIKE-001](../../docs/06-delivery/technical-spikes.md#spike-001--corpus-inventory-and-truth-protocol).
**The spike itself is in progress**: its exit gate (≥2,000 representative
images or an approved smaller pilot, with measured double-label agreement)
requires user-owned photographs and human labeling. This directory delivers
everything that can exist before that: the opt-in scanner, the truth
protocol, and the self-test evidence that the scanner is correct.

## Inventory scanner (opt-in, read-only, aggregates only)

```powershell
./spikes/SPIKE-001-corpus/OrientExpress.Spike001/bin/Release/net10.0/spike001.exe `
  scan --root <your-photo-tree> --results inventory.json [--max-files N]
```

- Never writes, moves, or decodes an image; it reads a bounded 256 KiB prefix
  per file with share-friendly handles and skips reparse points.
- Output contains **aggregates only**: counts by format, JPEG EXIF
  orientation histogram (tag 0x0112, both endiannesses), megapixel and size
  buckets, and camera-make counts. No paths, names, hashes, or thumbnails.
- Corrupt or unreadable files are counted, never fatal.

Use the aggregates to stratify a labeling tranche per the
[labeling guide](labeling-guide.md), then document it with the
[dataset card template](dataset-card-template.md).

## Verification

- 12 CI unit tests cover the format signatures and the EXIF/SOF/IHDR parsers
  (both TIFF endiannesses, missing EXIF, out-of-range orientation, corrupt
  segments).
- `spike001 self-test` builds a synthetic fixture tree (all 8 orientations,
  PNG, HEIC, corrupt, non-image) and verifies the full scan pipeline:
  [`results/spike001-selftest-results.json`](results/spike001-selftest-results.json).

## Remaining for the exit gate

1. Operator runs the scanner over opted-in photo trees.
2. Stratified tranche selection; consent recorded per source.
3. Double labeling + adjudication + kappa per the labeling guide.
4. Frozen tranche with manifest, labels, and dataset card → unlocks
   SPIKE-002/SPIKE-003 and later WP-007/WP-008/WP-020.
