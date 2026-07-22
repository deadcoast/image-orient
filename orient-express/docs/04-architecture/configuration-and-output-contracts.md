---
document_id: DOC-CONFIG
title: Configuration, Schemas, Events, and Compatibility
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-ARCH, ADR-0010]
traces_to: [PRD-FR-017, PRD-FR-018, PRD-FR-019, PRD-FR-020, PRD-FR-023, NFR-MAINT-002]
---

# Configuration and output contracts

## Precedence

Highest wins:

1. CLI option.
2. Environment variable `UPRIGHT_*`.
3. Explicit `--config`.
4. User config `%APPDATA%\orient-express\config.json`.
5. Built-in defaults.

Job plans store the normalized effective configuration relevant to decisions and mutation, plus its digest. Apply uses the sealed configuration; ordinary CLI options cannot silently replace it.

## Configuration groups

```json
{
  "schemaVersion": "1.0",
  "paths": {
    "jobRoot": "...",
    "modelRoot": "...",
    "cacheRoot": "...",
    "backupRoot": null
  },
  "discovery": {
    "recurse": true,
    "followReparsePoints": false,
    "include": ["**/*.jpg", "**/*.jpeg", "**/*.png"],
    "exclude": []
  },
  "analysis": {
    "profile": "conservative",
    "maxThumbnailLongEdge": 1280,
    "faceModel": "yunet-baseline",
    "sceneFallback": false
  },
  "output": {
    "mode": "safe-copy",
    "collision": "error",
    "jpegEdge": "abort",
    "metadataProfile": "preserve-personal"
  },
  "resources": {
    "memoryMiB": 1536,
    "metadataWorkers": 2,
    "inferenceWorkers": 1,
    "transformWorkers": 2
  },
  "privacy": {
    "telemetry": false,
    "reviewThumbnails": false,
    "redactPathsInDiagnostics": true
  }
}
```

The schema defines bounds, enums, defaults, and additional-property policy. Security-sensitive “expert” settings are grouped and warned.

## Environment mapping

Nested keys map predictably, for example:

```text
UPRIGHT_OUTPUT__MODE=safe-copy
UPRIGHT_RESOURCES__MEMORYMIB=2048
```

Secrets are not expected. Environment values are recorded only after redaction and normalization.

## External schemas

- [`config.schema.json`](../../schemas/config.schema.json)
- [`job-manifest.schema.json`](../../schemas/job-manifest.schema.json)
- [`event.schema.json`](../../schemas/event.schema.json)
- [`model-manifest.schema.json`](../../schemas/model-manifest.schema.json)
- [`review-decision.schema.json`](../../schemas/review-decision.schema.json)

These are design drafts at baseline 0.1. They freeze as v1 contracts only after spike/implementation review.

## Event envelope

All durable/machine events include:

- `schemaVersion`;
- `eventType`;
- `eventId`;
- timestamp in UTC;
- job/file/correlation identifiers where applicable;
- command/stage;
- result/status;
- reason codes;
- structured data;
- optional error object;
- provenance object.

Error objects expose safe messages and categorized details, not arbitrary native stderr. Diagnostic mode can include a redacted native excerpt in a separate field.

## Event families

- command started/completed;
- capability discovered;
- file discovered/inspected/assessed/planned;
- review required/decision appended;
- apply preflight/staged/verified/committed;
- retry/cancellation;
- recovery/undo;
- warning/error;
- aggregate progress/summary;
- model/config/job lifecycle.

## Reason-code policy

Reason codes are uppercase stable identifiers. They may be added in a minor schema release; removal/semantic reuse requires a major. Human messages may improve without breaking compatibility.

## JSON compatibility

- Consumers must ignore unknown additive fields only where schema permits.
- Enum expansion is potentially breaking for exhaustive consumers; announce and version appropriately.
- Required-field removal or type change is major.
- Numeric values define units in field names or descriptions.
- Timestamps are RFC 3339 UTC.
- Paths are strings plus optional normalized/path-token metadata; never native structs.
- Floating ML values are not contractual decision thresholds without policy provenance.

## Job compatibility

Apply checks:

- job manifest/schema major;
- plan seal and DB integrity;
- tool compatibility range;
- model/calibration/policy availability and digests;
- native adapter compatibility where required;
- effective config digest;
- filesystem capability revalidation.

A newer unsupported job can be inspected read-only, but not migrated or applied implicitly.

## Configuration safety

Invalid config fails before discovery. Unknown fields fail by default to catch typos. `--allow-unknown-config` may exist only for forward-compatible read-only commands and cannot enable mutation.

`config show --effective --redacted --json` is the canonical support view.
