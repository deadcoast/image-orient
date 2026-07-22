---
document_id: DOC-DOCGRAPH
title: Documentation Dependency Graph
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Architecture
depends_on: [DOC-GOVMODEL]
traces_to: []
---

# Documentation dependency graph

The rendered graph is generated from front matter in every Markdown document.

- [Open rendered SVG](../../diagrams/documentation-dependencies.svg)
- [Open Graphviz source](../../diagrams/documentation-dependencies.dot)
- [Open Mermaid overview](../../diagrams/documentation-dependencies.mmd)
- [Open document manifest](../../docs-manifest.json)

An arrow `A → B` means **B depends on A**. The graph should remain acyclic. Cross-document feedback is handled by change-impact review rather than circular prerequisite metadata.

## Reading order

Research and governance feed product/requirements; requirements and decisions feed architecture; architecture feeds quality and delivery; traceability spans all layers; the master specification summarizes the resulting baseline.
