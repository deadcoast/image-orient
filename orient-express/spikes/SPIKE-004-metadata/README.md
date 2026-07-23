# SPIKE-004 harness — metadata round-trip and ExifTool integration

Decision harness for [SPIKE-004](../../docs/06-delivery/technical-spikes.md#spike-004--metadata-round-trip-and-exiftool-integration):
qualifies the ExifTool `-stay_open` adapter (DEP-007, ADR-0006). Evidence
tooling, not the WP-005 metadata service.

## What it does

- **Adapter protocol prototype** (`ExifToolStayOpenClient`): one persistent
  `exiftool -stay_open True -@ -` process; typed operations only (no caller
  ever builds raw arguments); every command synchronized on `-execute<id>` /
  `{ready<id>}` **and** on a per-command `-echo4` stderr sentinel so
  diagnostics are attributable to the command that caused them.
- **Round-trip matrix** on synthetic baseline fixtures (cjpeg JPEG + managed
  PNG; no user photos): a rich EXIF/GPS/XMP/IPTC tag set is written, the
  orientation is changed with `-P -overwrite_original`, and the full `-j -G1
  -a -n` dumps are diffed — the only permitted change is `IFD0:Orientation`.
- **Operational behavior**: filesystem-timestamp restoration with/without
  `-P`; ADS and NTFS-file-id behavior across ExifTool's rewrite; JPEG marker
  structure stability; error-channel resilience (invalid write → error on
  synchronized stderr, process keeps serving); PNG XMP round-trip.

## Reproduce

```powershell
dotnet build OrientExpress.slnx -c Release
./spikes/SPIKE-004-metadata/OrientExpress.Spike004/bin/Release/net10.0/spike004.exe `
  run-evidence --results spikes/SPIKE-004-metadata/results/spike004-results.json `
  [--exiftool <path>] [--cjpeg <path>]
```

Raw results (with the ExifTool version and SHA-256):
[`results/spike004-results.json`](results/spike004-results.json). Findings and
guarantee language: [`report.md`](report.md). The 3 CI tests cover only the
tool-independent parts (fixtures, segment walker) — CI runners have no
exiftool; the evidence run is operator-driven.
