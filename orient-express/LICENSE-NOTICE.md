---
document_id: DOC-LICENSE
title: Licensing and Third-Party Notice Plan
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Release
depends_on: [DOC-SOURCES, DOC-CICD]
traces_to: []
---

# Licensing and notice plan

This repository is a planning artifact and does not bundle third-party binaries or models. Production distribution must not assume that a library license automatically covers its model files, delegates, codecs, or prebuilt executable.

## Required dependency intake fields

Each production dependency must record: name, exact version/digest, source URL, role, direct/transitive status, binary/model/doc classification, license expression, notice text, redistribution terms, export/security notes, update owner, and replacement path.

## High-attention candidates

- ExifTool executable and its associated files.
- YuNet or any replacement ONNX model, separately from OpenCV repository licensing.
- Windows ML/ONNX Runtime execution providers.
- libjpeg-turbo/jpegtran binaries.
- Magick.NET, ImageMagick, and any enabled delegate library.
- HEIC/AVIF codecs and Windows extension dependencies.
- SQLite native bundle.
- Installer/bootstrapper and signing tools.

No candidate becomes a production dependency until the dependency register and legal review are complete. See [research sources](docs/02-research/sources.md), [CI/CD and supply chain](docs/06-delivery/ci-cd-and-supply-chain.md), and ADRs.
