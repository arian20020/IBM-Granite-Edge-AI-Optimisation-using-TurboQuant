## Task 6 report — active tools and historical testing code split

Date: 2026-09-01
Worktree: `C:\Users\Student\Granite-Testing-Results-Cleanup-2026-09-01`
Commit message: `refactor: separate active and historical testing tools`

### Scope completed

- Moved tracked root-level maintained testing scripts into `scripts/testing/tools/`.
- Archived superseded root shims in `archive/testing-code/2026-09-01/` without rewriting their historical contents.
- Added `archive/testing-code/MIGRATION.csv` covering every tracked Task 1 root executable with its maintained destination category.
- Added `archive/testing-code/README.md` describing the maintained surfaces versus historical archive.
- Added `scripts/testing/tests/unit/test_code_migration_inventory.py` to enforce migration completeness, root cleanliness, and non-test import/path closure.
- Regenerated `docs/testing/cleanup/file-inventory.csv` from the original recovery source with updated Task 6 destinations and archive classifications.

### TDD record

1. Wrote `scripts/testing/tests/unit/test_code_migration_inventory.py` first.
2. Ran it before implementation and confirmed RED because `archive/testing-code/MIGRATION.csv` did not exist, the root was not clean, and active code still referenced legacy root paths.
3. Fixed one repo-root bug in the new test harness, reran, and reconfirmed RED for the intended missing migration state.
4. Performed the inventory-driven `git mv` split, retargeted active imports/path references, regenerated the inventory, and added the archive ledger.
5. Reran the new regression to GREEN, then expanded verification to the supported CLI surface and representative route suites.

### Implementation notes

- The maintained command implementations now live under `scripts/testing/tools/`, while the supported contributor surface remains `scripts/testing/cli/`.
- Historical root paths for `build_final_results.py` and `Export-Final-Results-Pdf.ps1` now map to canonical CLI entries in the migration ledger, and historical llama measurement entrypoints map to the campaign package.
- `measure_animehacker_server.py` and `Run-Animehacker-LargeHost.ps1` were archived as superseded shims with explicit replacements and `d0eb34f2` recorded as the last scientific baseline.
- `scripts/testing/tools/cleanup_inventory.py` now classifies the root-script exceptions so future inventory regeneration preserves the Task 6 mapping.

### Verification evidence

Focused migration regression:

- `python -m pytest scripts/testing/tests/unit/test_code_migration_inventory.py -v` -> `3 passed`

Final verification set on the final file state:

- `python -m pytest scripts/testing/tests/unit/test_code_migration_inventory.py scripts/testing/tests/unit/test_campaign_import_boundary.py scripts/testing/tests/unit/test_openvino_campaign_import_boundary.py scripts/testing/tests/unit/test_reporting_import_boundary.py scripts/testing/tests/integration/test_testing_cli.py scripts/testing/tests/test_atomicbot_runner.py scripts/testing/tests/test_animehacker_large_host.py scripts/testing/tests/test_run_official_openvino_quality.py scripts/testing/tests/test_publish_official_openvino_comparison.py -v` -> `119 passed`
- `python -m scripts.testing.cli.validate_results --route all --output-root docs/testing/final-results` -> validation passed; derivation reported `77 limitation(s)` and release metadata / release readiness reported `6 limitation(s)` each, matching the preserved collection.
- `git diff --check` -> exit `0`; only Git CRLF normalization warnings were emitted on Windows, with no whitespace errors.

### Self-review

- Confirmed `scripts/testing` root now contains only `README.md`, `requirements.txt`, and maintained directories.
- Confirmed active non-test Python code no longer imports or points at maintained legacy root script paths.
- Confirmed the CLI wrappers now dispatch to `scripts.testing.tools.*` modules instead of removed root files.
- Confirmed archived shims remain preserved under `archive/testing-code/2026-09-01/`.
- Left untracked recovery evidence and experimental raw-results content untouched.

### Concerns

- Git still reports informational CRLF-normalization warnings for edited text files on this Windows worktree. Verification remained green and no whitespace errors were reported.
