# Documentation tools

- `generate_graphs.py` regenerates Graphviz SVGs from `docs-manifest.json` and planning CSVs.
- `validate_docs.py` validates front matter, links, JSON/CSV, IDs, dependency cycles, trace references, and Must-requirement mappings.
- `validate-docs.ps1` is the Windows/PowerShell entry point.

Run from the repository root:

```powershell
pwsh ./tools/validate-docs.ps1
```

Graphviz must be installed for SVG regeneration. CI should run generation, fail on an unclean diff, then validate.
