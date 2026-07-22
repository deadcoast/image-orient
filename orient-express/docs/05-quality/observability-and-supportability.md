---
document_id: DOC-OBS
title: Observability, Diagnostics, and Supportability
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Operations
depends_on: [DOC-CONFIG, DOC-DATAMODEL, DOC-SEC]
traces_to: [PRD-FR-023, PRD-FR-028, NFR-OBS-001, NFR-OBS-002]
---

# Observability and supportability

## Design goals

A user or maintainer must be able to answer:

- What did the tool decide and why?
- Which files changed, did not change, or need review?
- Which model/runtime/provider/native tools and policies were active?
- Was the plan stale or the output verified?
- Where did time and resources go?
- Can an interrupted job resume, recover, or undo safely?
- Can support reproduce the issue without receiving the user's photos?

## Three outputs

1. **Durable job events:** canonical audit facts.
2. **Human/machine command output:** current invocation view.
3. **Diagnostic logs/bundles:** bounded troubleshooting detail.

Progress bars are not audit records.

## Event requirements

Every file reaches a terminal event with:

- job/file/correlation IDs;
- source path token;
- planned/effective action;
- result and reason codes;
- stage durations;
- warnings/errors;
- source and output fingerprints according to policy;
- model/runtime/provider/calibration/policy;
- transform/metadata/JPEG route;
- verification result.

## Logging

Use structured logging internally. Default file logs:

- exclude image bytes and tensors;
- redact or tokenize paths in export;
- rotate by size/count;
- use UTC timestamps;
- include process/build/job correlation;
- separate native stderr as bounded structured excerpts;
- avoid duplicate per-file noise at normal verbosity.

## Metrics

Per-job counters/gauges/histograms are emitted into summaries rather than a network service:

- discovered/eligible/planned by decision;
- review/unsupported/error reason distribution;
- stage throughput/latency;
- queue high-water marks;
- memory/worker settings;
- retries/native restarts;
- bytes read/written/backup;
- recovery/undo states;
- model/provider use.

Optional future OpenTelemetry export is out of v1 unless it is explicit and local/off by default.

## Diagnostic bundle

`upright jobs <job> diagnostics --output bundle.zip` is a future/proposed command or option. Baseline contents:

- tool/build and capability report;
- redacted manifest/effective config;
- schema/plan/job integrity summary;
- aggregate events and selected failed-row records;
- native/model version/digest inventory;
- OS/filesystem/provider information;
- bounded logs;
- no photos or review thumbnails by default.

User can preview a manifest before export. Full paths or sample files require explicit inclusion.

## Error taxonomy

Categories:

- argument/configuration;
- capability/compatibility;
- input inaccessible/malformed/unsupported;
- stale/conflict/safety preflight;
- metadata/codec/inference/native tool;
- output/verification;
- job/journal/recovery;
- cancellation/resource exhaustion;
- internal invariant.

Each error states retryability and recommended action.

## Support runbook outline

1. Run `doctor --json`.
2. Run `jobs integrity`.
3. Inspect terminal reason/event.
4. Confirm active model/native digests and capability class.
5. Reproduce with `inspect` or one-file safe-copy job.
6. Generate redacted diagnostics.
7. Never advise deleting backups/staging before journal reconciliation.
8. Escalate source-loss/verification/invariant failures as critical.

## Service levels

The project does not promise remote support SLAs in v1. It does promise that machine-readable diagnostics, version manifests, and recovery instructions are sufficient for deterministic issue triage.
