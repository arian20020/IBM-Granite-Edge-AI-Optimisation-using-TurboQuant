## Task 6 report — active tools and historical testing code split

Date: 2026-09-01
Worktree: `C:\Users\Student\Granite-Testing-Results-Cleanup-2026-09-01`
Commit message: `refactor: separate active and historical testing tools`

### Round 1 review findings — reproduced root cause before fixes

- Finding 1 reproduced on 2026-09-01 with `python scripts/testing/tools/run_openvino_reference_capability.py --help`, which failed immediately with `ModuleNotFoundError: No module named 'scripts'`.
- Root cause: the moved Python tool still computes `REPO_ROOT = SCRIPT_DIR.parent.parent`, which now resolves to `<repo>/scripts` from `scripts/testing/tools/` instead of the repository root, so direct execution no longer inserts the real repository root into `sys.path`.
- Root cause: five moved PowerShell tools still compute `$repoRoot` or `$controllerRepositoryRoot` from `$PSScriptRoot/../..`, which now resolves to `<repo>/scripts`. That breaks default paths such as `external/...`, `experiments/...`, and the wrapper lookup for `scripts/testing/campaigns/openvino/guarded_build.py` because they are resolved relative to the wrong ancestor after the Task 6 move.
- Finding 2 root cause: `scripts/testing/tests/unit/test_code_migration_inventory.py` only checked `ALLOWED_ROOT_DIRS.issubset(root_dirs)`, so an unexpected tracked directory at `scripts/testing/*` could coexist with the approved directories and still pass the regression.

### Round 1 fix record

- Added `scripts/testing/tests/unit/test_moved_tool_root_resolution.py` before production edits to cover:
  - direct `--help` execution for `scripts/testing/tools/run_openvino_reference_capability.py`
  - safe PowerShell evaluation of the moved-tool root/path calculations for `acquire_official_openvino.ps1`, `build_openvino_turboquant.ps1`, `invoke_guarded_command.ps1`, `prepare_openvino_cpu_observer_patch.ps1`, and `prepare_openvino_turboquant_patch.ps1`
- Tightened `scripts/testing/tests/unit/test_code_migration_inventory.py` so the tracked `scripts/testing` root directories must match the exact approved set while explicitly ignoring regenerable caches (`.pytest_cache`, `__pycache__`).
- Observed RED before production edits with:
  - `python -m pytest scripts/testing/tests/unit/test_moved_tool_root_resolution.py scripts/testing/tests/unit/test_code_migration_inventory.py scripts/testing/tests/test_official_openvino_patch_identity.py::PatchWorkspaceControllerTests::test_power_shell_entry_points_preserve_cwd_roots_overrides_and_exit -v`
  - Result: `5 failed, 3 passed`
  - The failures matched the review findings: direct Python execution imported from the wrong root, PowerShell root calculations resolved to `<repo>/scripts`, and the existing prepare-wrapper regression captured `cwd=...\\scripts` plus an `--evidence` path under `scripts\\external\\...`.
- Fixed the six moved-tool root calculations:
  - `scripts/testing/tools/run_openvino_reference_capability.py`
  - `scripts/testing/tools/acquire_official_openvino.ps1`
  - `scripts/testing/tools/build_openvino_turboquant.ps1`
  - `scripts/testing/tools/invoke_guarded_command.ps1`
  - `scripts/testing/tools/prepare_openvino_cpu_observer_patch.ps1`
  - `scripts/testing/tools/prepare_openvino_turboquant_patch.ps1`
- During focused verification, the dedicated wrapper regressions still referenced pre-move wrapper paths and one trusted-controller copy helper still created `scripts/testing/` without `scripts/testing/tools/`. Updated those test fixtures so the moved wrappers are exercised from their maintained locations instead of failing on stale paths.

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

Round 1 review-fix regressions:

- RED: `python -m pytest scripts/testing/tests/unit/test_moved_tool_root_resolution.py scripts/testing/tests/unit/test_code_migration_inventory.py scripts/testing/tests/test_official_openvino_patch_identity.py::PatchWorkspaceControllerTests::test_power_shell_entry_points_preserve_cwd_roots_overrides_and_exit -v` -> `5 failed, 3 passed`
- GREEN: `python -m pytest scripts/testing/tests/unit/test_moved_tool_root_resolution.py scripts/testing/tests/unit/test_code_migration_inventory.py scripts/testing/tests/test_official_openvino_patch_identity.py::PatchWorkspaceControllerTests::test_power_shell_entry_points_preserve_cwd_roots_overrides_and_exit -v` -> `8 passed`
- Additional moved-wrapper regression sweep: `python -m pytest scripts/testing/tests/test_build_openvino_turboquant_wrapper.py scripts/testing/tests/test_official_openvino_guarded_build.py -k "run_openvino_reference_capability or invoke_guarded_command or wrapper" -v` -> `38 passed, 27 deselected`

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
