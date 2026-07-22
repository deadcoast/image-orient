# External contract schemas

These JSON Schemas are **design drafts** for contract major version 1. They establish the shape that implementation and CLI contract tests will refine.

| Schema | Purpose |
|---|---|
| `config.schema.json` | User/effective configuration |
| `job-manifest.schema.json` | Durable job identity and compatibility |
| `event.schema.json` | JSONL/durable event envelope |
| `model-manifest.schema.json` | Verified ONNX model bundle |
| `review-decision.schema.json` | Append-only human override import |
| `plan-row.schema.json` | Sealed per-file plan contract |
| `capability-report.schema.json` | `doctor --json` operation capability report |

Rules:

- Never edit a released schema in place; publish a new version.
- Examples and golden files must validate in CI.
- Schema validity does not replace semantic invariant tests.
- Production `$id` URLs will be assigned before contract freeze.
