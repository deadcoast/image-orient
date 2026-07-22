---
document_id: DOC-SEC
title: Security, Privacy, and Threat Model
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Security
depends_on: [DOC-ARCH, DOC-FSSAFE, DOC-ML]
traces_to: [NFR-SEC-001, NFR-SEC-002, NFR-SEC-003, NFR-PRIV-001, NFR-PRIV-002, NFR-PRIV-003]
---

# Security and privacy

## Assets

- original photographs and embedded personal/location metadata;
- backups and corrected outputs;
- job plans, paths, reports, review decisions, thumbnails;
- model/native binaries and update channel;
- user trust in non-destructive behavior.

## Trust boundaries

1. CLI arguments, response files, environment, and config.
2. Filesystem paths and image/metadata bytes.
3. Native decoders, ExifTool, jpegtran, and model runtime.
4. Model bundles and job/review imports.
5. Installer/update artifacts.
6. Optional diagnostics export.

## Threats and controls

| Threat | Controls |
|---|---|
| Path traversal/output escape | Canonical root containment, handle identity, no trust in relative path strings |
| Symlink/reparse/hardlink alias attack | Default no traversal, handle-based identity, alias detection, capability refusal |
| Malformed image exploit | Patched/pinned codecs, dimension/byte limits, fuzzing, timeouts, process isolation where feasible |
| Decompression bomb | Header probe, pixel/frame/page limits, bounded decode, memory governor |
| Metadata command injection | No shell, typed allowlisted arguments, protocol framing, literal path handling |
| Native-tool hang/crash | Timeout, cancellation, kill tree, bounded stdout/stderr, restart/quarantine |
| Model supply-chain attack | Digest/signature, ONNX/operator/resource policy, provenance, no executable code in bundle |
| Job/review tampering | Schemas, seals/digests, append-only events, path/root revalidation |
| Source replacement race | Handle identity/fingerprint immediately before mutation, stage/commit protocol |
| Backup deletion/prune abuse | Retention locks, dry-run, unresolved-journal protection |
| Log/privacy leakage | No image bytes, thumbnails opt-in, path redaction, bounded retention |
| Unwanted network transfer | No core network dependency, network-denied tests, explicit update/install commands |
| DLL search hijacking | Safe DLL loading, absolute verified paths, controlled install directory |
| Installer/update tampering | Authenticode, checksums, provenance/SBOM, secure channel, rollback |
| Plugin code execution | No arbitrary in-process plugins v1 |

## Parser and resource limits

Before expensive decode/inference:

- maximum file bytes (configurable);
- maximum dimensions/pixels/frames/pages;
- maximum metadata response size/tag count/depth;
- maximum JSON/config/job/review sizes;
- path length and input count;
- model file size/tensor shape/operator constraints;
- per-stage timeout and retry budget.

Limits produce stable reason codes.

## Native dependency policy

- Prefer memory-safe managed orchestration.
- Run standalone tools without a shell and with minimal inherited environment.
- Use restricted working directories and explicit current directory.
- Verify binary digest before first use and record version.
- Capture stdout/stderr separately and redact paths in support output.
- Track CVEs/advisories and patch supported release branches.
- Evaluate Windows sandbox/AppContainer/job-object restrictions; do not claim sandboxing until proven compatible.

## Privacy architecture

- All analysis local and offline.
- No account required.
- No face recognition or identity embedding.
- No cross-file identity clustering.
- No default telemetry.
- Review thumbnails disabled by default.
- Diagnostic bundles default to path tokens and omit photos/thumbnails.
- Model updates are explicit; core does not fetch silently.
- Jobs contain local sensitive paths and should inherit user-only ACLs.

## Data retention

Job, backup, thumbnail, log, and model cache retention are separate policies. `jobs prune` is dry-run by default. Deletion uses ordinary filesystem semantics; secure erasure is not promised on SSD/cloud filesystems.

## Security testing

- fuzz image/metadata/config/job/review/model parsers;
- malicious path/reparse/hardlink tests;
- dependency scanning and SBOM;
- static analysis and .NET analyzers;
- native binary verification;
- clean-user/standard-user execution;
- network-denied E2E;
- installer privilege and uninstall tests;
- threat-model review at M1, M4, and RC.

## Incident response

Release manifests make every native/model dependency identifiable. A security issue can disable/quarantine a model/adapter, publish an advisory, rotate signing keys, ship a patched bundle, and preserve job readability. Safety defects may break compatibility only with explicit migration/diagnostic instructions.

## Privacy non-claims

The product is not a biometric system because it does not identify people, but face landmarks and photos remain sensitive personal data. Documentation must avoid implying that “local” removes the need for secure storage and consent.
