# Unified final-results validation summary

Overall result for release package `unified-final-results-2026-09-01`: **Passed — ready with documented limitations**.

| Gate | Result | Blocking findings | Limitations |
| --- | --- | ---: | ---: |
| `schema` | Passed | 0 | 0 |
| `ids` | Passed | 0 | 0 |
| `coverage` | Passed | 0 | 0 |
| `derivation` | Passed | 0 | 77 |
| `status_failure_consistency` | Passed | 0 | 0 |
| `availability` | Passed | 0 | 0 |
| `paths_hashes` | Passed | 0 | 0 |
| `claim_coverage` | Passed | 0 | 0 |
| `workbook_parity` | Passed | 0 | 0 |
| `pdf_structure` | Passed | 0 | 0 |
| `comparability` | Passed | 0 | 0 |
| `release_metadata` | Passed | 0 | 5 |
| `release_readiness` | Passed | 0 | 5 |

The 77 `derivation` limitations are source-retained canonical summary metrics that cannot be derived from canonical measurement columns: 39 KV-allocation summaries (13 upstream llama.cpp, 19 AtomicBot, and 7 animehacker), plus 19 AtomicBot CPU-utilization and 19 AtomicBot GPU-utilization summaries. They are real retained summaries, not missing or non-executed observations and not zero substitutions. Release limitations cover absent verified licensing and author metadata, guarded cross-route comparability, the no-rerun/no-raw-mutation boundary, and canonical release authority. Full structured evidence, including all 202 inspected PDF pages and both read-only workbook audits, is in [release-readiness.json](release-readiness.json).

The top manifest hashes canonical staged Git blobs and excludes itself. Release package `unified-final-results-2026-09-01`, that manifest, and the enclosing Git commit jointly identify the release. Protected uncommitted AtomicBot working bytes are intentionally outside that authority.
