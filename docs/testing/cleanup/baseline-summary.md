# Testing cleanup baseline

This checkpoint freezes the testing and results state before any structural move,
archive copy, or cleanup removal. It was produced read-only from the protected
recovery worktree and the implementation worktree.

## Source identities

| Role | Branch | Commit |
|---|---|---|
| Protected recovery evidence | `testing/openvino-turboquant-recovery` | `5ec36c25826c03533f274e19b248d1c011b05de2` |
| Cleanup implementation | `testing/results-cleanup-v1` | `4af0d2719f658346d768f32bf2d33e2c942c7fab` |

The absolute roots, exact porcelain-v2 status hashes, and approved scope roots are
recorded in `inventory-metadata.json`. Absolute paths are provenance metadata only;
they are not unrestricted cleanup targets.

## Inventory

| Measure | Count |
|---|---:|
| Files inventoried | 5,057 |
| Tracked | 3,404 |
| Untracked | 1,045 |
| Ignored | 608 |
| Retain active | 1,429 |
| Move active | 235 |
| Archive externally (proposed) | 1,128 |
| Remove regenerable (proposed) | 401 |
| Retain ambiguous | 1,864 |

No proposed archive or removal action has been executed. Ambiguous files remain
retained. `source-canonical-deltas.csv` separately records 4,287 exact files, 215
different files, 555 recovery-only files, and 99 implementation-only files.

## Frozen semantic invariants

| Invariant | Frozen value |
|---|---:|
| Planned outcomes | 169 |
| Passed | 81 |
| Failed | 6 |
| Blocked | 28 |
| Artifact unavailable | 54 |
| Evidence-index records | 1,857 |
| Unique cited evidence paths | 1,846 |
| Markdown/DOCX report pairs | 6 |
| PDFs | 6 |
| Searchable, nonblank PDF pages | 202 |

`baseline-semantic-snapshot.json` preserves stable IDs, scientific tables, evidence
relationships, report identities, PDF identities, and the complete comparability
matrix. Its self-excluding canonical snapshot hash detects semantic drift without
depending on the future directory layout.

## Safety boundary

This baseline did not rerun inference, benchmarks, model conversion, or quality
adjudication. The inventory and semantic snapshot are classification and validation
artifacts only. External archival and source removal remain blocked until every
candidate is copied, hash-verified, receipted, and reconciled in later plan tasks.
