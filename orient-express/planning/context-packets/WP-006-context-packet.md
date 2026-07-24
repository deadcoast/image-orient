# WP-006 context packet — model registry and inference runtime

Generated 2026-07-24 per DOC-DEVPLAN ("Work-package context packet").

## Objective

Productionize the SPIKE-003-qualified inference stack behind the
architecture: versioned digest-pinned model bundles with manifests
(`schemas/model-manifest.schema.json`), quarantine-on-failure install
(AC-038), atomic rollback-safe activation (AC-039), the `IInferenceRuntime`
port (DOC-COMPONENTS shape) on ONNX Runtime CPU, working
`models list/install/verify/activate/rollback/remove` commands, and doctor
truth-telling for runtime and active model.

## Mapped requirements and acceptance

- **PRD-FR-020** (Must): model lifecycle with digest-pinned bundles,
  versioned manifests, rollback-safe activation — AC-038, AC-039.
- **NFR-SEC-002**: every artifact digest-verified at install *and* at load.
- **NFR-PRIV-001**: no network anywhere; install consumes local files only.

## Upstream spike conclusions (binding)

SPIKE-003: ONNX Runtime 1.27.1 CPU EP is the v1 baseline (deterministic,
1.64 ms median, offline); DirectML/Windows ML gated. DEP-006 rule: digest
verified on every load, no mutable "latest". SPIKE-002 pilot: YuNet 2023mar
has a fixed 640×640 input; preprocessing is BGR 0–255 stretch (recorded in
the shipped manifest).

## Design constraints (DOC-ARCH layering)

- `OrientExpress.Contracts`: manifest records (permissive parse,
  code-validated required fields/enums), `RuntimeDescriptor`, `ModelHandle`,
  tensor IO records — package-free.
- `OrientExpress.Application`: `IModelRegistry` and `IInferenceRuntime`
  ports.
- `OrientExpress.Infrastructure.Inference`: file-based registry under the
  configured `paths.modelRoot` (`<id>/<version>/` + `active.json` pointer
  written atomically via temp+replace; quarantine directory for AC-038
  failures) and `OnnxInferenceRuntime` (session cache keyed by digest,
  digest re-verified at load).
- `OrientExpress.Cli`: `models` subtree un-gated with real handlers; model
  root comes from the WP-003 effective configuration; doctor components
  updated. `models rollback` is an additive contract extension (ADR-0010
  minor) required by PRD-FR-020's rollback language.

## Threat/risk notes

Model bundles are untrusted input (NFR-SEC-002/threat model): digest
mismatch, malformed manifest, unsupported schema/task, or incompatible
runtime all route to quarantine with typed reasons and can never be
activated (AC-038). Load-time digest re-verification catches post-install
tampering. No signature verification yet (packaging-time concern, WP-023) —
recorded as a remainder.

## Test and evidence plan

Registry integration tests (hermetic temp roots): install happy path;
digest-mismatch → quarantined + unlistable + unactivatable; manifest/
runtime incompatibility → quarantined; activation pointer atomic with prior
recorded; rollback restores prior (AC-039); remove refuses the active
model; verify detects post-install tamper. Runtime tests: descriptor and a
deterministic forward pass against the cached YuNet (skip when absent).
CLI golden tests (hermetic `HostEnvironment`): full lifecycle through
`upright models …` with exit-code mapping, plus doctor truth.

## Dependencies and consumers

Microsoft.ML.OnnxRuntime 1.27.1 (already pinned, DEP-005). Consumers:
WP-007 (orientation engine consumes the runtime port + active model),
WP-020 (release calibration), WP-022 (bundle packaging).

## Out of scope

The orientation engine and YuNet post-processing (WP-007 —
`postprocessing.id` in the manifest names the decoder the engine must
implement); signature verification (WP-023); provider fallback beyond CPU
(WP-021); calibration artifacts (WP-008).

## Definition of done

Validator/build/format/tests green; AC-038/AC-039 implemented and tested;
`models` commands work end to end against the cached YuNet artifact;
doctor truth-tells; statuses, STATUS, CHANGELOG, manifest updated.
