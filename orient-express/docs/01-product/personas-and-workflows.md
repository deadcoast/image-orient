---
document_id: DOC-WORKFLOWS
title: Personas and Operational Workflows
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Product
depends_on: [DOC-CHARTER]
traces_to: [PRD-FR-002, PRD-FR-007, PRD-FR-008, PRD-FR-009, PRD-FR-010, PRD-FR-014, PRD-FR-015, PRD-FR-016, PRD-FR-017]
---

# Personas

## P1 — Personal library owner

Needs guided defaults, understandable summaries, no source risk, and a manageable review list.

## P2 — Automation-oriented developer

Needs pure JSON/JSONL, stable exit codes, response files, deterministic plans, idempotency, and reproducible versions.

## P3 — Digital archive operator

Needs metadata fidelity, provenance, safe-copy roots, checksums, audit reports, retention policy, and explicit unsupported cases.

## Core workflows

### W1 — Diagnose environment

```powershell
upright doctor
upright doctor --json > capabilities.json
upright inspect "C:\Photos\sample.jpg"
```

Outcome: no image writes; capability and proposed-decision explanation.

### W2 — Conservative two-phase batch

```powershell
upright scan "C:\Photos" --job "C:\UprightJobs\2026-07-21" --output "D:\Photos-Upright"
upright review "C:\UprightJobs\2026-07-21" list
upright apply "C:\UprightJobs\2026-07-21"
upright verify "C:\UprightJobs\2026-07-21"
```

Outcome: immutable plan, optional human overrides, safe-copy output, verification report.

### W3 — Unattended automation

```powershell
upright scan @inputs.rsp --job .\job --jsonl 1>events.jsonl 2>progress.log
if ($LASTEXITCODE -in 0, 10) {{ upright apply .\job --jsonl }}
```

Exit code `10` is proposed for “completed with REVIEW items”; final values are governed by the CLI spec.

### W4 — Guarded in-place cleanup

```powershell
upright scan "C:\Photos" --job .\job --mode in-place --backup-root "E:\UprightBackups"
upright apply .\job --acknowledge-in-place
upright jobs .\job integrity
```

The plan reports required backup space and blockers before apply.

### W5 — Review without GUI

```powershell
upright review .\job export --format csv --output decisions.csv
# User edits only the documented decision column.
upright review .\job import decisions.csv
upright apply .\job
```

### W6 — Recovery and undo

```powershell
upright jobs .\job integrity
upright apply .\job --resume
upright undo .\job
```

Recovery reconciles journal state. Undo refuses if the post-apply file changed independently.

## Usability rules

- Use “upright”, “rotate”, and “normalize metadata”; avoid presenting aspect ratio as the decision.
- Every refusal includes a reason code and next action.
- Dry-run/scan summaries show counts, disk estimates, unsupported cases, review items, and active policies.
- Progress is suppressed or redirected when stdout is machine-readable.
