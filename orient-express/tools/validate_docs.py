#!/usr/bin/env python3
"""Validate the PhotoUpright dev-spec as an executable documentation set."""
from __future__ import annotations

import argparse
import collections
import csv
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote

FRONT_START = "---\n"
LINK_RE = re.compile(r"(?<!!)\[[^\]]*\]\(([^)]+)\)")
ID_RE = re.compile(r"^(?:DOC|GOAL|PRD-FR|NFR-[A-Z]+|AC|COMP|ADR|SPIKE|WP|TEST-[A-Z]+|RISK|SRC|DEP)-[A-Z0-9-]+$|^M\d+$")

def parse_frontmatter(path: Path) -> dict | None:
    text = path.read_text(encoding="utf-8")
    if not text.startswith(FRONT_START):
        return None
    end = text.find("\n---\n", len(FRONT_START))
    if end < 0:
        raise ValueError(f"{path}: unterminated front matter")
    result: dict[str, object] = {}
    for lineno, line in enumerate(text[len(FRONT_START):end].splitlines(), 2):
        if not line.strip():
            continue
        if ":" not in line:
            raise ValueError(f"{path}:{lineno}: invalid front matter line")
        key, raw = line.split(":", 1)
        key, raw = key.strip(), raw.strip()
        if raw.startswith("[") and raw.endswith("]"):
            inner = raw[1:-1].strip()
            result[key] = [] if not inner else [item.strip() for item in inner.split(",")]
        else:
            result[key] = raw
    return result

def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))
    if not rows and path.name not in {"dependency-register.csv"}:
        raise ValueError(f"{path}: no data rows")
    if rows:
        expected = set(rows[0])
        for idx, row in enumerate(rows, 2):
            if set(row) != expected or None in row:
                raise ValueError(f"{path}:{idx}: inconsistent CSV width")
    return rows

def cycles(nodes: set[str], edges: list[tuple[str, str]]) -> list[list[str]]:
    graph: dict[str, list[str]] = collections.defaultdict(list)
    for source, target in edges:
        graph[source].append(target)
    color = {node: 0 for node in nodes}
    stack: list[str] = []
    found: list[list[str]] = []
    def visit(node: str) -> None:
        color[node] = 1
        stack.append(node)
        for nxt in graph.get(node, []):
            if nxt not in color:
                continue
            if color[nxt] == 0:
                visit(nxt)
            elif color[nxt] == 1:
                start = stack.index(nxt)
                found.append(stack[start:] + [nxt])
        stack.pop()
        color[node] = 2
    for node in sorted(nodes):
        if color[node] == 0:
            visit(node)
    return found

def validate(root: Path) -> tuple[list[str], dict[str, int]]:
    errors: list[str] = []
    stats: dict[str, int] = {}

    # Markdown/front matter and local links.
    documents: list[dict] = []
    doc_by_id: dict[str, dict] = {}
    markdown_files = sorted(root.rglob("*.md"))
    stats["markdown_files"] = len(markdown_files)
    for path in markdown_files:
        try:
            meta = parse_frontmatter(path)
        except Exception as exc:
            errors.append(str(exc))
            continue
        if meta is not None:
            required = {"document_id", "title", "version", "status", "as_of", "owner", "depends_on", "traces_to"}
            missing = required - set(meta)
            if missing:
                errors.append(f"{path}: missing front matter keys {sorted(missing)}")
                continue
            doc_id = str(meta["document_id"])
            if doc_id in doc_by_id:
                errors.append(f"duplicate document ID {doc_id}: {path} and {doc_by_id[doc_id]['path']}")
            entry = {"path": path, **meta}
            doc_by_id[doc_id] = entry
            documents.append(entry)

        text = path.read_text(encoding="utf-8")
        for raw_target in LINK_RE.findall(text):
            target = raw_target.strip().split(" ", 1)[0].strip("<>")
            if not target or target.startswith(("#", "http://", "https://", "mailto:", "data:")):
                continue
            target = unquote(target.split("#", 1)[0])
            resolved = (path.parent / target).resolve()
            try:
                resolved.relative_to(root.resolve())
            except ValueError:
                errors.append(f"{path}: local link escapes package root: {raw_target}")
                continue
            if not resolved.exists():
                errors.append(f"{path}: broken local link: {raw_target}")

    stats["frontmatter_documents"] = len(documents)
    doc_ids = set(doc_by_id)
    doc_edges: list[tuple[str, str]] = []
    for entry in documents:
        for dep in entry.get("depends_on", []):
            if dep not in doc_ids:
                errors.append(f"{entry['path']}: unknown document dependency {dep}")
            else:
                doc_edges.append((dep, entry["document_id"]))
    for cycle in cycles(doc_ids, doc_edges):
        errors.append("documentation dependency cycle: " + " -> ".join(cycle))

    # JSON syntax and manifest consistency.
    json_files = sorted(root.rglob("*.json"))
    stats["json_files"] = len(json_files)
    parsed_json: dict[Path, object] = {}
    for path in json_files:
        try:
            parsed_json[path] = json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:
            errors.append(f"{path}: invalid JSON: {exc}")
    manifest_path = root / "docs-manifest.json"
    manifest = parsed_json.get(manifest_path)
    if isinstance(manifest, dict):
        manifest_docs = {item.get("document_id"): item for item in manifest.get("documents", [])}
        if set(manifest_docs) != doc_ids:
            errors.append("docs-manifest.json document IDs do not match front-matter documents")
        for doc_id, entry in doc_by_id.items():
            m = manifest_docs.get(doc_id, {})
            rel = entry["path"].relative_to(root).as_posix()
            if m.get("path") != rel:
                errors.append(f"docs-manifest.json path mismatch for {doc_id}")
        if manifest.get("document_count") != len(documents):
            errors.append("docs-manifest.json document_count mismatch")
    else:
        errors.append("docs-manifest.json missing or not an object")

    # CSV catalogs.
    csv_files = sorted((root / "planning").glob("*.csv"))
    stats["csv_files"] = len(csv_files)
    catalogs: dict[str, list[dict[str, str]]] = {}
    for path in csv_files:
        try:
            catalogs[path.name] = read_csv(path)
        except Exception as exc:
            errors.append(str(exc))

    known_ids = set(doc_ids)
    id_sources = {
        "requirements-catalog.csv": "id",
        "component-catalog.csv": "id",
        "spike-register.csv": "id",
        "backlog.csv": "id",
        "milestones.csv": "id",
        "test-matrix.csv": "id",
        "risk-register.csv": "id",
        "research-sources.csv": "id",
        "decision-register.csv": "id",
        "technology-dependencies.csv": "id",
        "acceptance-criteria.csv": "id",
    }
    global_seen: dict[str, str] = {}
    # Requirement/acceptance IDs intentionally appear in convenience catalogs, so check duplicates within each.
    for filename, field in id_sources.items():
        seen: set[str] = set()
        for row in catalogs.get(filename, []):
            ident = row.get(field, "")
            if not ident:
                errors.append(f"{filename}: row missing {field}")
                continue
            if ident in seen:
                errors.append(f"{filename}: duplicate ID {ident}")
            seen.add(ident)
            known_ids.add(ident)
            if not ID_RE.match(ident):
                errors.append(f"{filename}: unexpected ID shape {ident}")

    # Trace and dependency references.
    trace_rows = catalogs.get("traceability-matrix.csv", [])
    stats["trace_edges"] = len(trace_rows)
    for idx, row in enumerate(trace_rows, 2):
        for key in ("from_id", "to_id"):
            ident = row.get(key, "")
            if ident not in known_ids:
                errors.append(f"traceability-matrix.csv:{idx}: unknown {key} {ident}")

    dep_rows = catalogs.get("dependency-register.csv", [])
    stats["dependency_edges"] = len(dep_rows)
    for idx, row in enumerate(dep_rows, 2):
        for key in ("from_id", "to_id"):
            ident = row.get(key, "")
            if ident not in known_ids:
                errors.append(f"dependency-register.csv:{idx}: unknown {key} {ident}")

    # Implementation cycles: component graph and delivery/spike graph.
    comp_nodes = {r["id"] for r in catalogs.get("component-catalog.csv", [])}
    comp_edges = [(r["from_id"], r["to_id"]) for r in dep_rows if r.get("edge_type") == "implementation"]
    for cycle in cycles(comp_nodes, comp_edges):
        errors.append("component dependency cycle: " + " -> ".join(cycle))

    delivery_nodes = (
        {r["id"] for r in catalogs.get("spike-register.csv", [])}
        | {r["id"] for r in catalogs.get("backlog.csv", [])}
    )
    delivery_edges = [
        (r["from_id"], r["to_id"]) for r in dep_rows
        if r.get("edge_type") in {"delivery", "spike-gate"}
        and r.get("from_id") in delivery_nodes and r.get("to_id") in delivery_nodes
    ]
    for cycle in cycles(delivery_nodes, delivery_edges):
        errors.append("delivery dependency cycle: " + " -> ".join(cycle))

    # Must requirement coverage.
    relations: dict[tuple[str, str], set[str]] = collections.defaultdict(set)
    for row in trace_rows:
        relations[(row.get("from_id", ""), row.get("relation", ""))].add(row.get("to_id", ""))
    req_rows = catalogs.get("requirements-catalog.csv", [])
    ac_by_req = collections.defaultdict(set)
    for row in req_rows:
        if row.get("record_type") == "acceptance_criterion":
            ac_by_req[row.get("parent", "")].add(row.get("id", ""))
    tests_by_ac_or_req = collections.defaultdict(set)
    for row in catalogs.get("test-matrix.csv", []):
        if row.get("acceptance"):
            tests_by_ac_or_req[row["acceptance"]].add(row["id"])
        if row.get("requirement"):
            tests_by_ac_or_req[row["requirement"]].add(row["id"])

    must_functional = [
        r for r in req_rows
        if r.get("record_type") == "functional_requirement" and r.get("priority") == "Must"
    ]
    stats["must_functional_requirements"] = len(must_functional)
    for req in must_functional:
        rid = req["id"]
        acs = ac_by_req.get(rid, set())
        if not acs:
            errors.append(f"{rid}: Must functional requirement has no acceptance criteria")
        if not relations.get((rid, "realized_by")):
            errors.append(f"{rid}: no component realization mapping")
        if not relations.get((rid, "implemented_by")):
            errors.append(f"{rid}: no work-package mapping")
        if acs and not any(tests_by_ac_or_req.get(ac) for ac in acs):
            errors.append(f"{rid}: no test mapped through acceptance criteria")

    must_nfrs = [
        r for r in req_rows
        if r.get("record_type") == "nonfunctional_requirement" and r.get("priority") == "Must"
    ]
    stats["must_nfrs"] = len(must_nfrs)
    for req in must_nfrs:
        rid = req["id"]
        if not relations.get((rid, "implemented_by")):
            errors.append(f"{rid}: no work-package mapping")
        if not tests_by_ac_or_req.get(rid):
            errors.append(f"{rid}: no direct NFR evidence test")

    # Front-matter trace references must point at known IDs.
    for entry in documents:
        for ident in entry.get("traces_to", []):
            if ident not in known_ids:
                errors.append(f"{entry['path']}: unknown traces_to ID {ident}")

    # Required package paths.
    required_paths = [
        "README.md", "MASTER-SPEC.md", "STATUS.md", "docs-manifest.json",
        "planning/traceability-matrix.csv", "planning/backlog.csv",
        "schemas/config.schema.json", "schemas/job-manifest.schema.json",
        "diagrams/documentation-dependencies.svg",
        "diagrams/implementation-dependencies.svg",
        "tools/generate_graphs.py",
    ]
    for rel in required_paths:
        if not (root / rel).exists():
            errors.append(f"missing required artifact: {rel}")

    return errors, stats

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("root", nargs="?", default=str(Path(__file__).resolve().parents[1]))
    args = parser.parse_args()
    root = Path(args.root).resolve()
    errors, stats = validate(root)
    print("PhotoUpright dev-spec validation")
    for key in sorted(stats):
        print(f"  {key}: {stats[key]}")
    if errors:
        print(f"\nFAILED with {len(errors)} issue(s):", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1
    print("\nPASS: documentation, catalogs, schemas, links, dependencies, and traceability are internally consistent.")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
