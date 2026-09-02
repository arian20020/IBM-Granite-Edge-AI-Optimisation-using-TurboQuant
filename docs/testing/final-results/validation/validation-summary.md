# Unified final-results validation summary

Overall result for release package `unified-final-results-2026-09-01-v2`: **Passed — ready with documented limitations**.

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
| `release_metadata` | Passed | 0 | 6 |
| `release_readiness` | Passed | 0 | 6 |

The 77 `derivation` limitations are source-retained canonical summary metrics that cannot be derived from canonical measurement columns: 39 KV-allocation summaries (13 upstream llama.cpp, 19 AtomicBot, and 7 animehacker), plus 19 AtomicBot CPU-utilization and 19 AtomicBot GPU-utilization summaries. They are real retained summaries, not missing or non-executed observations and not zero substitutions. Release limitations cover absent verified licensing and author metadata, guarded cross-route comparability, the no-rerun/no-semantic-evidence-mutation boundary, the disclosed EOL-only canonical-byte admission for scoped frozen text, canonical release authority, and the experimental XLSX formula-display limitation in non-calculating readers until Excel recalculation. Full structured evidence, including all 202 inspected PDF pages and four read-only workbook audits, is in [release-readiness.json](release-readiness.json).

The top manifest is generated last from canonical staged Git blobs and excludes itself. Release package `unified-final-results-2026-09-01-v2`, that manifest, and the enclosing Git commit jointly identify the release. A raw Git archive contains all 1,846 unique cited evidence paths (1,857 relationship records) and validates without external hydration. Protected uncommitted AtomicBot working bytes are intentionally outside that authority.

The implementation branch no longer carries the 693 tracked legacy raw paths already classified `archive_external`. Their 125,662 authoritative bytes remain verified in the external archive, while all parent-Git identities—including 128 EOL-only normalized blobs—are recorded in the cleanup receipt. The canonical 1,400-path retained library and every published evidence relationship remain exact.
