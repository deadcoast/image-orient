---
document_id: DOC-IMPLGRAPH
title: Implementation Dependency Graph
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-SPIKES, DOC-WBS, DOC-ARCH]
traces_to: []
---

# Implementation dependency graph

- [Open rendered SVG](../../diagrams/implementation-dependencies.svg)
- [Open Graphviz source](../../diagrams/implementation-dependencies.dot)
- [Open Mermaid source](../../diagrams/implementation-dependencies.mmd)
- [Open dependency register](../../planning/dependency-register.csv)

The graph combines:

- component prerequisites;
- spike gates;
- work-package dependencies;
- milestone grouping.

An arrow points from prerequisite to dependent. CI rejects cycles in the spike/work-package execution graph.

## Critical path

`SPIKE-001 → SPIKE-002 → WP-007 → WP-008 → WP-009 → WP-010/WP-013 → WP-014 → WP-015 → WP-020 → WP-024`.

This path is not the only work. Foundations, packaging, security, and operational UX proceed in parallel only when their dependency closure is satisfied.
