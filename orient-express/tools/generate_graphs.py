#!/usr/bin/env python3
"""Regenerate Graphviz dependency diagrams from the dev-spec manifest and CSV catalogs."""
from __future__ import annotations
import argparse
import csv
import json
import re
import shutil
import subprocess
from collections import defaultdict
from pathlib import Path

def q(value: str) -> str:
    return '"' + str(value).replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n") + '"'

def read_csv(path: Path):
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        return list(csv.DictReader(handle))

def render(dot_path: Path) -> None:
    dot = shutil.which("dot")
    if not dot:
        raise RuntimeError("Graphviz 'dot' executable was not found.")
    svg_path = dot_path.with_suffix(".svg")
    subprocess.run([dot, "-Tsvg", str(dot_path), "-o", str(svg_path)], check=True)

def documentation_graph(root: Path, manifest: dict) -> None:
    entries = manifest["documents"]
    colors = {
        "root":"#f3f4f6","00-governance":"#e0f2fe","01-product":"#dcfce7",
        "02-research":"#fef3c7","03-requirements":"#ede9fe","04-architecture":"#dbeafe",
        "05-quality":"#fce7f3","06-delivery":"#ffedd5","07-traceability":"#ccfbf1",
        "08-adrs":"#f5f5f4"
    }
    titles = {
        "root":"Root","00-governance":"Governance","01-product":"Product",
        "02-research":"Research","03-requirements":"Requirements","04-architecture":"Architecture",
        "05-quality":"Quality","06-delivery":"Delivery","07-traceability":"Traceability","08-adrs":"ADRs"
    }
    def cat(path: str) -> str:
        return path.split("/")[1] if path.startswith("docs/") else "root"
    lines = [
        "digraph DocumentationDependencies {",
        '  graph [rankdir=LR, bgcolor="white", pad="0.2", nodesep="0.25", ranksep="0.7", compound=true];',
        '  node [shape=box, style="rounded,filled", fontname="Arial", fontsize=9, color="#475569", margin="0.08,0.05"];',
        '  edge [color="#64748b", arrowsize=0.6, penwidth=0.8];'
    ]
    for category in titles:
        selected = [e for e in entries if cat(e["path"]) == category]
        if not selected:
            continue
        cluster = re.sub(r"[^A-Za-z0-9]", "_", category)
        lines += [f"  subgraph cluster_{cluster} {{", f"    label={q(titles[category])};", '    color="#cbd5e1"; style="rounded";']
        for e in selected:
            lines.append(f'    {q(e["document_id"])} [label={q(e["document_id"] + chr(10) + e["title"])}, fillcolor={q(colors[category])}];')
        lines.append("  }")
    for e in entries:
        for dep in e.get("depends_on", []):
            lines.append(f'  {q(dep)} -> {q(e["document_id"])};')
    lines.append("}")
    path = root / "diagrams/documentation-dependencies.dot"
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    render(path)

def implementation_graph(root: Path) -> None:
    spikes = read_csv(root / "planning/spike-register.csv")
    backlog = read_csv(root / "planning/backlog.csv")
    milestones = read_csv(root / "planning/milestones.csv")
    by_phase = defaultdict(list)
    for item in backlog:
        by_phase[item["milestone"]].append(item)
    lines = [
        "digraph ImplementationDependencies {",
        '  graph [rankdir=LR, bgcolor="white", pad="0.2", nodesep="0.18", ranksep="0.8", compound=true, newrank=true];',
        '  node [shape=box, style="rounded,filled", fontname="Arial", fontsize=9, color="#475569", margin="0.08,0.05"];',
        '  edge [color="#64748b", arrowsize=0.6, penwidth=0.9];',
        '  subgraph cluster_spikes { label="Risk-first technical spikes"; color="#f59e0b"; style="rounded";'
    ]
    for s in spikes:
        lines.append(f'    {q(s["id"])} [label={q(s["id"] + chr(10) + s["title"])}, fillcolor="#fef3c7"];')
    lines.append("  }")
    for m in milestones:
        lines += [f'  subgraph cluster_{m["id"]} {{ label={q(m["id"] + " — " + m["name"])}; color="#94a3b8"; style="rounded";']
        lines.append(f'    {q(m["id"])} [shape=oval, label={q(m["id"] + " gate")}, fillcolor="#e2e8f0"];')
        for w in by_phase[m["id"]]:
            lines.append(f'    {q(w["id"])} [label={q(w["id"] + chr(10) + w["title"])}, fillcolor="#dbeafe"];')
        lines.append("  }")
    for w in backlog:
        for dep in filter(None, w["depends_on"].split(";")):
            lines.append(f'  {q(dep)} -> {q(w["id"])};')
        lines.append(f'  {q(w["id"])} -> {q(w["milestone"])} [style=dashed, color="#94a3b8"];')
    lines.append("}")
    path = root / "diagrams/implementation-dependencies.dot"
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    render(path)

def component_graph(root: Path) -> None:
    comps = read_csv(root / "planning/component-catalog.csv")
    lines = [
        "digraph ComponentDependencies {",
        '  graph [rankdir=LR, bgcolor="white", pad="0.2", nodesep="0.25", ranksep="0.7"];',
        '  node [shape=box, style="rounded,filled", fillcolor="#e0f2fe", fontname="Arial", fontsize=9, color="#475569"];',
        '  edge [color="#64748b", arrowsize=0.6];'
    ]
    for c in comps:
        lines.append(f'  {q(c["id"])} [label={q(c["id"] + chr(10) + c["name"])}];')
    for c in comps:
        for dep in filter(None, c["depends_on"].split(";")):
            lines.append(f'  {q(dep)} -> {q(c["id"])};')
    lines.append("}")
    path = root / "diagrams/component-dependencies.dot"
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    render(path)

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", nargs="?", default=str(Path(__file__).resolve().parents[1]))
    args = parser.parse_args()
    root = Path(args.root).resolve()
    manifest = json.loads((root / "docs-manifest.json").read_text(encoding="utf-8"))
    documentation_graph(root, manifest)
    implementation_graph(root)
    component_graph(root)
    render(root / "diagrams/traceability-overview.dot")
    print("Generated documentation, implementation, component, and traceability SVG diagrams.")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
