# Machine-readable planning catalogs

| File | Purpose |
|---|---|
| `requirements-catalog.csv` | Goals, functional requirements, NFRs, and acceptance criteria |
| `traceability-matrix.csv` | Goal → requirement → acceptance/architecture/work/test/milestone edges |
| `component-catalog.csv` | Component responsibilities and prerequisites |
| `spike-register.csv` | Technical proof gates |
| `backlog.csv` | Work packages and dependency order |
| `milestones.csv` | Entry/exit evidence gates |
| `test-matrix.csv` | Planned verification evidence |
| `risk-register.csv` | Risks, triggers, mitigations, response |
| `research-sources.csv` | Source/evidence register |
| `decision-register.csv` | ADR index |
| `dependency-register.csv` | Component and delivery graph edges |
| `technology-dependencies.csv` | Candidate production/build dependencies and intake status |
| `acceptance-criteria.csv` | Convenience acceptance view |

UTF-8 with BOM is used for CSV compatibility with Windows spreadsheet tools. Semicolon-delimited ID lists are intentional inside cells.
