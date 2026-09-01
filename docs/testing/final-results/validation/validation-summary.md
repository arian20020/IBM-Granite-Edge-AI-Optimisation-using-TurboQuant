# Unified final-results validation summary

Overall result for the committed clean snapshot: **Passed — ready with documented limitations**.

| Gate | Result | Blocking findings | Limitations |
| --- | --- | ---: | ---: |
| `schema` | Passed | 0 | 0 |
| `ids` | Passed | 0 | 0 |
| `coverage` | Passed | 0 | 0 |
| `derivation` | Passed | 0 | 77 |
| `status_failure_consistency` | Passed | 0 | 0 |
| `availability` | Passed | 0 | 0 |
| `paths_hashes` | Passed | 0 | 2 |
| `claim_coverage` | Passed | 0 | 0 |
| `workbook_parity` | Passed | 0 | 0 |
| `pdf_structure` | Passed | 0 | 0 |
| `comparability` | Passed | 0 | 0 |
| `release_metadata` | Passed | 0 | 6 |
| `release_readiness` | Passed | 0 | 6 |

The 77 `derivation` limitations are retained, explicit cases where a missing or non-executed observation has no measurement to derive; they are not treated as zero. The `paths_hashes` limitations are two retained source-evidence line-ending portability findings. Release limitations cover absent verified licensing and author metadata, guarded cross-route comparability, the no-rerun/no-raw-mutation boundary, the committed-clean-snapshot authority, and those two portability findings. Full structured evidence, including all 202 inspected PDF pages and both read-only workbook audits, is in [release-readiness.json](release-readiness.json).

Validation of a live worktree with protected uncommitted AtomicBot bytes is expected to differ at exactly the corresponding two top-manifest hashes. The committed clean snapshot, not those unknown live bytes, is the release authority.
