---
document_id: DOC-CICD
title: CI/CD, Reproducibility, and Software Supply Chain
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Release
depends_on: [DOC-SEC, DOC-TEST, DOC-GOVMODEL]
traces_to: [NFR-SEC-002, WP-001, WP-019, WP-022, WP-023]
---

# CI/CD and supply chain

## Pipeline stages

### Pull request

1. restore from locked/pinned dependencies;
2. formatting/analyzers/warnings-as-errors;
3. documentation/schema/traceability validation;
4. build all supported configurations;
5. unit/property/architecture/contract tests;
6. CLI/config/event golden tests;
7. fast metadata/media fixtures;
8. dependency/license/vulnerability checks;
9. publish test results and coverage (coverage is diagnostic, not sole gate).

### Main/nightly

- full integration/native tests;
- Windows filesystem fault subset;
- fuzz regression corpora;
- provider equivalence subset;
- media/metadata golden matrix;
- package smoke on clean VM;
- performance trend;
- 10k/100k synthetic job tests;
- SBOM/provenance generation.

### Release candidate

- frozen corpus evaluation;
- full fault matrix;
- security/fuzz review;
- 100k soak and performance qualification;
- full compatibility environments;
- clean-VM install/update/rollback/uninstall;
- signing, SBOM, checksums, provenance;
- traceability evidence pack;
- release approval.

## Build reproducibility

- pin .NET SDK via `global.json`;
- central package versions and NuGet lock files;
- deterministic build/path mapping;
- exact native/model downloads by digest into controlled build cache;
- do not fetch mutable “latest” URLs during release;
- record compiler/runtime/OS/container/runner image;
- compare artifact hashes where build system permits;
- separate signing step from reproducible unsigned artifact production.

## Dependency intake

Every direct/transitive runtime/native/model/tool dependency enters `planning/dependency-register.csv` or the production equivalent with:

- exact version/digest/source;
- owner and purpose;
- license/notice;
- security support/update policy;
- binary/model provenance;
- runtime privileges/network/file access;
- replacement/exit strategy;
- qualification tests.

Automated vulnerability alerts do not replace manual review of native/model supply chains.

## Signing and secrets

- Authenticode certificate/private key is stored in managed signing service/HSM where available.
- CI jobs requesting signing are protected, isolated, auditable, and only run for approved tags/commits.
- No secrets in repository, logs, diagnostics, or package.
- Checksums are generated after final signed artifact formation and published through a trusted channel.
- Key rotation and revocation procedure is documented/tested.

## SBOM and provenance

Generate SPDX SBOM covering managed, native, tool, model, installer, and license artifacts. Model files are first-class components, not opaque data. Provenance identifies source commit, workflow, dependencies, model digests, and build environment.

## Artifact retention

- PR artifacts: bounded retention.
- Spike/evaluation/fault/release evidence: long-lived immutable retention.
- Private corpus/raw images: separate restricted store, not general CI artifacts.
- Signed releases and SBOM/provenance: permanent project retention.
- Test logs are redacted and bounded.

## Release branch policy

Prefer tagged mainline release. Supported patch branches receive critical security/safety fixes. A dependency/model update is a behavior change and runs appropriate full gates even when application source barely changes.

## Documentation CI

`tools/validate_docs.py` and graph generation run on every change. CI fails for:

- broken links/JSON/CSV;
- duplicate or unknown IDs;
- implementation dependency cycles;
- Must requirement without acceptance/test/work mapping;
- ADR/source/dependency references missing;
- schema examples invalid;
- unregenerated manifest/graphs.

## Supply-chain incident response

Ability to identify affected releases/jobs from manifests, revoke/quarantine an asset, disable it in capability policy, publish patched signed artifacts, preserve rollback, and update notices/advisories.
