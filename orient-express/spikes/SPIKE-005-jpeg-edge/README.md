# SPIKE-005 harness — JPEG lossless rotation edge policy

Decision harness for [SPIKE-005](../../docs/06-delivery/technical-spikes.md#spike-005--jpeg-lossless-rotation-edge-policy):
qualifies jpegtran (libjpeg-turbo, DEP-008, ADR-0007) on adversarial MCU-edge
dimensions. Evidence tooling, not the WP-010 transform engine.

## What it does

- Generates synthetic, orientation-sensitive gradient fixtures (managed PPM
  writer; no user photos), encodes them with `cjpeg` at 4:4:4 (`-sample 1x1`,
  8×8 iMCU) and 4:2:0 (`-sample 2x2`, 16×16 iMCU).
- Runs the full matrix — dimensions {512×512, 500×500, 512×500, 500×512,
  511×511, 15×15} × samplings × rotations {90, 180, 270} × modes {default,
  `-perfect`, `-trim`} — 108 cases, recording exit codes, output dimensions,
  decode deltas, and marker behavior (`-copy none` vs `-copy all`).
- **Losslessness is proven at the coefficient level**: each successful
  rotation is inverted with jpegtran and its decode must be byte-identical to
  the original decode. Decode-vs-reference-rotation deltas are recorded
  separately because integer IDCT and chroma upsampling are *not*
  rotation-equivariant (a key verification finding — see the report).
- Safety expectations: `-perfect` success ⇒ coefficient-lossless round-trip;
  aligned 512×512 always accepted by `-perfect`; decode delta ≤ 4; `-trim`
  output dimensions are MCU-derived (visible, opt-in); default mode always
  produces output.

## Reproduce

```powershell
dotnet build OrientExpress.slnx -c Release
./spikes/SPIKE-005-jpeg-edge/OrientExpress.Spike005/bin/Release/net10.0/spike005.exe `
  run-evidence --results spikes/SPIKE-005-jpeg-edge/results/spike005-results.json `
  [--tools-dir C:\libjpeg-turbo64\bin]
```

Raw results: [`results/spike005-results.json`](results/spike005-results.json)
(includes the tool SHA-256 digests). Findings and the recommended policy:
[`report.md`](report.md). 5 CI unit tests prove the managed reference
rotation before it judges jpegtran.
