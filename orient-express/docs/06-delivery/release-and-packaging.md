---
document_id: DOC-RELEASE
title: Release, Packaging, Installation, Update, and Rollback
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Release
depends_on: [DOC-CICD, DOC-COMPAT, DOC-SEC]
traces_to: [WP-022, WP-023, WP-024, SPIKE-010]
---

# Release and packaging

## Release artifacts

Baseline release set:

- self-contained `win-x64` portable ZIP;
- signed installer after qualification;
- detached SHA-256 checksums;
- Authenticode signatures for executable/installer and eligible native binaries;
- SPDX SBOM;
- dependency/license notices;
- provenance/build manifest;
- model bundle manifest/digests;
- JSON Schemas and CLI contract reference;
- release notes, compatibility matrix, known limitations;
- optional signature/attestation artifacts.

## Directory layout

```text
PhotoUpright/
  upright.exe
  *.dll
  runtimes/
  tools/
    exiftool/
    jpegtran/
  models/
    <model-id>/<version>/
  schemas/
  notices/
  LICENSES/
  README.txt
```

Exact native/runtime assets are determined by spikes. Search paths are explicit and verified; the current working directory is not trusted for DLL/tool resolution.

## Installation modes

### Portable

Unpack to a user-controlled directory; no administrator rights required where possible. Data/config/jobs/models default under user profile locations. Portable mode supports offline use and side-by-side rollback.

### Installer

Candidate installer must:

- install per-user by default unless machine-wide is explicitly selected;
- expose PATH integration as opt-in;
- install/uninstall cleanly;
- preserve user jobs/config/models unless explicitly selected for removal;
- register no always-running service;
- verify prerequisites or bundle them;
- support repair and rollback;
- display notices and install location;
- avoid unrequested file associations.

MSIX, WiX/Burn, or another technology is selected by SPIKE-010 based on unpackaged native/runtime needs, signing, update, enterprise deployment, and rollback.

## Models and optional providers

Core release includes a verified baseline model where licensing permits. Optional model/provider install is explicit:

```powershell
upright models install <bundle-or-approved-source>
upright models verify <id>
upright models activate <id>
```

Downloads, if implemented, use HTTPS, digest/signature, size limits, atomic activation, and offline package alternatives. Core orientation processing never fetches dependencies automatically.

## Versioning

- Product uses semantic versioning for public CLI/contracts.
- Schemas have independent major/minor versions.
- Model/calibration/policy use immutable IDs/digests and compatibility ranges.
- Native dependency versions are recorded exactly.
- Build metadata identifies commit and reproducible build inputs.

## Update policy

No silent background updater in v1. `upright version` may report local version only by default. An explicit future `update check` can access the network with clear consent. Updating the app cannot automatically activate behavior-changing model/policy without compatibility/evaluation gates.

## Rollback

- Portable: retain prior directory/package and model activation pointer.
- Installer: validate supported downgrade/repair path.
- Jobs from newer incompatible major versions remain diagnostically readable where possible.
- Model activation is atomic with prior model retained.
- Release rollback drill covers app, model, calibration/policy, and installer.

## Clean-VM qualification

For every artifact:

1. verify signatures/checksums;
2. install/unpack as standard user;
3. run `doctor`;
4. inspect fixtures offline;
5. scan/apply/verify safe-copy;
6. run cancellation/resume;
7. test Unicode/long paths;
8. update/repair/rollback;
9. uninstall;
10. verify no source/jobs removed unexpectedly and no orphan service/startup task.

## Release channels

- `preview`: experimental, separate config/job root recommended, no in-place guarantee unless stated.
- `stable`: passed all gates.
- Emergency security/safety patch: may disable a risky adapter/feature and must provide migration/diagnostics.

## Release approval

Product, Architecture, ML Quality, Security, and Release each approve their gate. No single role can waive a critical source-safety or false-rotation failure.
