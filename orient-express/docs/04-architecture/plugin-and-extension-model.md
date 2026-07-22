---
document_id: DOC-PLUGIN
title: Plugin and Extension Model
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-ARCH, ADR-0011]
traces_to: [NFR-SEC-003, NFR-MAINT-001]
---

# Plugin and extension model

## v1 policy

orient-express will not load arbitrary third-party assemblies or native DLLs into the CLI process. The image parser/native/tool attack surface and the destructive nature of file operations make an unrestricted plugin system unjustified for v1.

## Extensibility built into the core

Internal ports allow first-party or reviewed replacements for:

- metadata service;
- image probe/decoder/transformer;
- inference runtime;
- model bundle;
- confidence calibrator/decision policy;
- file capability/commit service;
- report renderer.

Adapters are selected through a composition root and capability policy, not dynamic reflection over user directories.

## Model extensibility

Model bundles are data, not executable plugins, but are still untrusted. A bundle requires:

- versioned manifest;
- ONNX format and allowed operator/runtime compatibility;
- exact digest/signature;
- tensor names/shapes/dtypes;
- preprocessing/postprocessing ID;
- license/provenance;
- calibration/policy compatibility;
- resource limits;
- corpus evaluation and activation approval.

No custom Python, DLL, or arbitrary script is executed from a model bundle.

## Future external-process protocol

If demand is proven, v2 may support an opt-in local JSON-RPC/stdio plugin protocol:

- separate process and explicit executable path;
- handshake with protocol/capabilities/schema;
- bounded messages and timeouts;
- no direct job DB access;
- file access through explicit paths/handles or controlled temporary artifacts;
- stable typed request/response;
- kill/restart isolation;
- signed/allowlisted plugin policy for mutating use;
- read-only capabilities can be authorized separately from transforms.

Third-party plugin output can propose evidence or transform artifacts but cannot bypass central policy, verification, journal, or commit.

## Extension governance

A new adapter/plugin point requires an ADR, threat model, contract tests, compatibility policy, and at least two independent implementations or a concrete replacement need. “Future flexibility” alone is not sufficient.
