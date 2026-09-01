## Task 7 report — test-suite responsibility layout

Date: 2026-09-01
Worktree: `C:\Users\Student\Granite-Testing-Results-Cleanup-2026-09-01`
Branch: `testing/results-cleanup-v1`
Starting HEAD: `16e425ea9d7c60812f1cb6f04f1a469897fc7e43`
Commit message: `test: organize testing verification by responsibility`

### Recovery audit

- Recovery began from the existing staged and working-tree state; no reset, checkout, stash, deletion, or re-move was performed.
- The index already contained the pre-move collection manifest, the migration CSV, the layout regression, and the responsibility moves. The working tree contained path/import fixes, documentation/hash updates, a post-move manifest, and separate unstaged Task 6 moved-tool path repairs.
- A prior Python process was still running as PID `11908` with command `python -m pytest scripts/testing/tests/acceptance/test_final_results_pdf_export.py -x -vv`. It started at `2026-09-01 21:52:02`; at `21:54:03` it was responsive with `1.4375` CPU seconds, and it later exited without intervention. Its result was not observable and is not credited as verification.
- That prior run explains the apparent stall point: the targeted module contains 13 Windows/Word PDF-export tests, including repeated `tasklist.exe` Word enumeration, Word COM startup, bounded 60-second exports, cleanup observation, and timeout cases. This recovery did not enumerate Word and did not rerun that module.
- `.boundary-controller-35jba_9i` was absent at the initial targeted audit and at the final audit. No such path was removed or modified.
- All 1,020 untracked files reported at final audit remain under `experiments/`; no scientific or raw-results evidence was removed, staged, or rewritten.

### Scope completed

- Classified all 86 tracked `test_*.py` files into `unit`, `integration`, or `acceptance` in `docs/testing/cleanup/test-path-migration.csv`.
- Moved the tests with Git rename tracking and updated moved-test repository roots, fixture paths, cross-test imports, and maintained command paths.
- Added `scripts/testing/tests/unit/test_test_path_migration.py` to enforce one migration row per tracked test, unique source/destination paths, valid responsibility buckets, destination naming, and normalized collection identity.
- Preserved `scripts/testing/tests/fixtures/` in place.
- Updated maintained final-results reproduction commands and their nested/root SHA-256 manifests. An active Markdown audit found no remaining maintained root-level `scripts/testing/tests/test_*.py` command reference.
- Regenerated `docs/testing/cleanup/post-move-pytest-collection.txt` from the final moved tree.

### Collection-equivalence evidence

- Pre-move manifest: `1,838 tests collected in 1.24s`.
- Fresh post-move command: `python -m pytest scripts/testing/tests --collect-only -q` -> exit `0`, `1,838 tests collected in 0.77s`.
- Independent manifest comparison: 86 migration rows; 1,838 pre-move node IDs; 1,838 post-move node IDs; all node IDs unique; normalized sets equal; 0 pre-only and 0 post-only IDs.
- Layout regression: `python -m pytest scripts/testing/tests/unit/test_test_path_migration.py -q` -> `1 passed in 0.06s`.

### Safe suite results

No benchmark command was run.

- Unit: `python -m pytest scripts/testing/tests/unit -q` -> `348 passed, 14 skipped in 30.21s`.
- Initial unrestricted integration: `python -m pytest scripts/testing/tests/integration -q` -> `1,157 passed, 1 skipped, 5 failed in 733.55s`.
  - Four failures were in RAM-dependent format-boundary quality cases; their preserved guard receipts recorded `3,672,666,112` available bytes and `quality prompt launch requires at least 4096 MiB RAM` at `available-ram-admission`.
  - The fifth failure was the configured-RAM-floor wrapper test. Its receipt recorded the same below-floor condition, `termination_reason=minimum_available_ram_before_launch`, and a null exit code after the safety block.
  - Follow-up system readings remained below the immutable 4 GiB floor: `4,202,602,496` bytes and later `3,853,770,752` bytes. The floor was not lowered or bypassed.
- Final resource-safe integration: the same integration suite with the three proven RAM-dependent quality groups and the configured-RAM-floor wrapper node deselected -> `1,155 passed, 1 skipped, 7 deselected in 993.84s`.
  - The seven deselections comprise one reconciliation success case, four parameterizations of the reconciliation mutation case, one real projected-U4 quality flow, and one configured-RAM-floor wrapper case.
- Initial safe acceptance, excluding the entire Word-enumerating PDF-export module: `299 passed, 1 failed in 204.52s`.
  - The sole failure was index/working-tree skew in the canonical release-manifest test because Task 7 documentation and hashes had not yet been staged.
- Focused acceptance after the brief's scoped staging command: `test_release_manifest_matches_git_index_canonical_blobs_for_archive_portability` -> `1 passed, 5 warnings in 13.31s`.
- Final staged-state safe acceptance: `python -m pytest scripts/testing/tests/acceptance -q --ignore=scripts/testing/tests/acceptance/test_final_results_pdf_export.py` -> `300 passed, 5 warnings in 199.70s`.
- The five acceptance warnings are third-party SWIG deprecation warnings for `SwigPyPacked`, `SwigPyObject`, and `swigvarlink`.
- `git diff --cached --check` -> exit `0`, with no whitespace errors.

### Preserved out-of-scope recovery state

The following unstaged tracked edits were deliberately not included by Task 7's scoped `git add` command. Each repairs a Task 6 legacy tool path and remains available for separate reconciliation:

- `scripts/testing/campaigns/openvino/adaptive_campaign.py`
- `scripts/testing/tools/adjudicate_official_openvino_adaptive_quality.py`
- `scripts/testing/tools/adjudicate_official_openvino_quality.py`
- `scripts/testing/tools/measure_official_openvino.py`

Task 7 verification ran against the exact working tree containing those preserved edits. They are therefore an explicit follow-up concern when evaluating the Task 7 commit in isolation.

### Concerns

- The Word/PDF acceptance module was not rerun because the recovery instruction forbade Word enumeration; the prior unobserved process result is not counted.
- Seven integration cases could not be safely exercised to their intended success paths while free RAM remained below 4 GiB. Their safety-block behavior and receipts were captured, and every other integration case passed in the explicit resource-safe run.
- The four unstaged Task 6 moved-tool path repairs above remain outside this Task 7 commit and must not be discarded.
- Git emitted informational LF-to-CRLF normalization warnings for edited text files on Windows; staged whitespace validation passed.
