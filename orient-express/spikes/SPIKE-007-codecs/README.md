# SPIKE-007 harness — format and codec capability matrix

Decision harness for [SPIKE-007](../../docs/06-delivery/technical-spikes.md#spike-007--format-and-codec-capability-matrix):
qualifies the WIC baseline (DEP-009) and defines operation-level capability
reporting. Evidence tooling, not the WP-005 codec adapter. Windows TFM +
WPF (WIC-backed) — the only spike project with a TFM override.

## What it does

- **Operational probes**: encode a synthetic bitmap → decode it back →
  dimension-exact round-trip, per format (JPEG/PNG/TIFF/BMP/GIF), through
  WPF's WIC wrappers; plus a JPEG EXIF-orientation read via the WIC metadata
  query language against an ExifTool-authored value.
- **Header-engagement probes** (the reliable presence test): feed
  `BitmapDecoder` a truncated real header per format and classify —
  component-not-found ⇒ `CODEC_NOT_INSTALLED`; a format/data rejection ⇒ the
  codec is present and engaged. Needs no sample files.
- **Findings recorded**: the registry `Instance` categories list only add-on
  codecs, and managed `ComImport` enumeration of WIC components returned
  empty — decode-attempt probing is the production capability mechanism.

## Reproduce

```powershell
dotnet build OrientExpress.slnx -c Release
./spikes/SPIKE-007-codecs/OrientExpress.Spike007/bin/Release/net10.0-windows/spike007.exe `
  run-evidence --results spikes/SPIKE-007-codecs/results/spike007-results.json
```

Raw results: [`results/spike007-results.json`](results/spike007-results.json).
Findings, the baseline matrix, and licensing inventory: [`report.md`](report.md).
