---
document_id: ADR-0004
title: Use ONNX behind a runtime abstraction
version: 0.1.0
status: accepted
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-RESEARCH, DOC-SRS]
traces_to: [ADR-0004]
---

# ADR-0004: Use ONNX behind a runtime abstraction

**Status:** Accepted  
**Decision date:** 2026-07-21

## Context

Hardware and Windows AI deployment options evolve. Binding the domain directly to one inference provider would make model policy and packaging fragile.

## Decision

Use ONNX as the model format and an application-owned `IInferenceRuntime`. Qualify Windows ML self-contained as preferred and direct ONNX Runtime CPU as deterministic fallback.

## Alternatives considered

OpenCV DNN and MediaPipe remain candidates but are not the architectural boundary. Framework-native PyTorch/TensorFlow runtime was rejected for package size and production complexity.

## Consequences

Provider/runtime can change behind contracts; preprocessing/postprocessing/model manifests remain owned by the app. Provider equivalence and provenance are mandatory.

## Proof / review gate

SPIKE-003 measures package size, latency, cold start, failure fallback, offline setup, and decision equivalence.

## Revisit triggers

- The named proof gate fails or a materially better supported alternative appears.
- Security, licensing, compatibility, or maintenance status changes.
- The product scope changes in a way that invalidates the original context.

This ADR may be superseded but its ID and rationale remain in the historical decision register.
