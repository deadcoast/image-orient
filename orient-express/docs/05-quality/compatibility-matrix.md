---
document_id: DOC-COMPAT
title: Compatibility and Qualification Matrix
version: 0.1.0
status: baseline
as_of: 2026-07-21
owner: Quality
depends_on: [DOC-SCOPE, DOC-FSSAFE, DOC-METADATA, DOC-TEST]
traces_to: [PRD-FR-021, NFR-COMP-001, NFR-COMP-002]
---

# Compatibility matrix

This is the qualification plan, not a claim that every row already passes.

## Operating system/runtime

| Dimension | Baseline | Qualification |
|---|---|---|
| OS | Supported Windows 11 x64 | Current serviced builds, standard user, long paths |
| Runtime | Self-contained .NET 10 LTS | Current patch at release |
| Shell | PowerShell 7 and Windows PowerShell 5.1; cmd.exe basic | quoting, redirection, exit codes, Unicode |
| CPU | x64 with supported .NET/native requirements | older/newer instruction sets |
| GPU/NPU | Optional | provider-specific matrix; CPU fallback always tested |

## Filesystem/storage

| Class | Safe-copy | In-place | Notes |
|---|---|---|---|
| Local NTFS | Target guarantee | Target guarantee after SPIKE-006 | Full fault matrix |
| Local ReFS | Qualify | Provisional | Replace/metadata semantics tested |
| exFAT/FAT/removable | Qualify | Default disabled | weak identity/atomicity/ACL behavior |
| SMB/network share | Read/safe-copy only if tested | Default disabled | SQLite/WAL and atomic semantics differ |
| OneDrive hydrated local | Qualify | Default disabled initially | placeholder/recall/race behavior |
| Offline placeholder | Report/hydrate explicit only | No | no surprise network |
| Reparse/symlink | No traversal default | No | explicit expert read-only future |
| Hardlinks | Deduplicate/report | Default refuse | mutation affects aliases |
| EFS/compressed | Qualify | Provisional | backup/replace attributes |
| ADS present | Preserve/report according to policy | Requires proof | data-loss risk |

## Media operations

| Format | Analyze | Safe-copy transform | In-place | Guaranteed metadata |
|---|---|---|---|---|
| JPEG | Must | Must | Must after gates | governed profile |
| PNG | Must | Must | Must after gates | governed profile |
| TIFF | Should/probe | optional | disabled until matrix | operation-specific |
| WebP | Should/probe | optional | disabled until matrix | operation-specific |
| HEIC/HEIF | optional | optional | disabled initially | capability-specific |
| AVIF | optional | optional | disabled initially | capability-specific |
| RAW | optional read metadata | no | no | read-only |
| Animated/multipage | report | no default | no | no frame-loss promise |

## Provider qualification

For each CPU/DirectML/vendor provider:

- install/offline availability;
- model operator compatibility;
- cold/warm latency and memory;
- output numeric comparison;
- decision equivalence on frozen corpus;
- concurrency/thread safety;
- cancellation/failure fallback;
- driver/OS support;
- provenance and rollback.

A fast provider that changes accepted decisions beyond tolerance is disabled or separately calibrated/versioned.

## Locale/path matrix

- non-ASCII Latin, CJK, emoji, combining characters;
- spaces, parentheses, `&`, leading/trailing-sensitive names;
- long paths over 260 characters;
- case-only collisions;
- reserved device names rejected safely;
- DST/timezone/locale-independent timestamps and numeric JSON;
- right-to-left display considerations in human output.

## Qualification evidence

Each supported matrix cell links to a test environment manifest and result artifact. “Unknown” is reported as unsupported/provisional, not assumed from a library feature list.
