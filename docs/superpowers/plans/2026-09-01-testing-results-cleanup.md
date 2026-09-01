# Testing and Results Repository Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the cluttered testing, raw-evidence, and final-results trees with a supported CLI, evidence-driven retained core, verified external historical archive, and consistent six-part published-route layout without changing any tested result.

**Architecture:** Work from a new linked worktree descended from the approved design commit. Generate immutable baseline inventories and semantic snapshots before moving anything. Perform Git-aware code and results migrations behind stable internal interfaces, copy and verify external archive candidates before any removal, then prove old/new semantic equivalence and validate an untouched Git archive.

**Tech Stack:** Python 3.11, pytest, PowerShell 7, Git, CSV/JSON/JSON-LD, SHA-256, openpyxl, python-docx, PyMuPDF, pypdf, jsonschema.

**Spec:** `docs/superpowers/specs/2026-09-01-testing-results-cleanup-design.md`

## Global Constraints

- Begin implementation in a new linked worktree and branch based on the commit containing this plan; use `d0eb34f2fca9e1bc00e176f325195e9b7b663a18` as the scientific-results comparison baseline.
- Treat the current recovery worktree as a separate read-only evidence source. Record its absolute path and starting status, inventory its in-scope tracked, modified, untracked, and ignored files, and never assume the clean implementation worktree contains those bytes.
- Never rerun a model benchmark or quality adjudication.
- Preserve exactly 169 outcomes: 81 passed, 6 failed, 28 blocked, and 54 artifact-unavailable.
- Preserve all stable route, campaign, case, attempt, measurement, summary, quality, failure, deviation, claim, and evidence IDs.
- Preserve all scientific values, statuses, quality methods, comparison classifications, report claims, and source bytes.
- Preserve 1,846 cited evidence relationships with exact content SHA-256 and sizes after path migration.
- Preserve six Markdown/DOCX parity relationships and six PDFs totaling 202 searchable, nonblank pages.
- Preserve original OpenVINO XLSX files as evidence-only and portable derivatives with zero machine-specific absolute paths.
- Never infer that a failed or superseded attempt is disposable; classification follows evidence and reproduction relationships.
- Copy and hash-verify every external archive candidate before removing its source.
- Do not overwrite an existing external archive; choose a numbered sibling on collision.
- Never use `git reset --hard`, `git clean`, blanket checkout restoration, wildcard deletion, or force worktree removal.
- Never stage, overwrite, or discard unrelated dirty or untracked work from the recovery worktree.
- Never touch or enumerate unrelated Word processes.
- Use `apply_patch` for authored edits, `git mv` for pure tracked renames, and exact verified paths for mechanical archive operations.
- Every task ends with focused tests, a scoped commit, and independent review before the next task.

---

### Task 1: Freeze the cleanup inventory and semantic baseline

**Files:**
- Create: `scripts/testing/tools/__init__.py`
- Create: `scripts/testing/tools/cleanup_inventory.py`
- Create: `scripts/testing/tools/cleanup_semantics.py`
- Create: `scripts/testing/tests/unit/test_cleanup_inventory.py`
- Create: `scripts/testing/tests/unit/test_cleanup_semantics.py`
- Create: `docs/testing/cleanup/file-inventory.csv`
- Create: `docs/testing/cleanup/duplicate-groups.csv`
- Create: `docs/testing/cleanup/baseline-semantic-snapshot.json`
- Create: `docs/testing/cleanup/baseline-summary.md`

**Interfaces:**
- Produces: `InventoryRecord`, `Classification`, `build_inventory(source_root: Path, canonical_root: Path) -> tuple[InventoryRecord, ...]`.
- Produces: `build_semantic_snapshot(repo_root: Path) -> dict[str, object]`.
- Produces: deterministic CSV/JSON artifacts consumed by Tasks 6, 12, 13, and 14.

- [ ] **Step 1: Write inventory classification tests**

```python
def test_inventory_preserves_referenced_failure_and_archives_unreferenced_run(tmp_path):
    repo = make_cleanup_fixture(tmp_path)
    rows = build_inventory(repo)
    by_path = {row.path: row for row in rows}
    assert by_path["experiments/raw-results/run-failed/stderr.log"].action == "retain_active"
    assert by_path["experiments/raw-results/run-abandoned/debug.tmp"].action == "archive_external"
    assert by_path["scripts/testing/__pycache__/runner.pyc"].action == "remove_regenerable"
```

Also assert deterministic path ordering, tracked/untracked/ignored status, SHA-256, size, evidence IDs, duplicate groups, and `retain_ambiguous` precedence.

- [ ] **Step 2: Run the tests to verify RED**

```powershell
& $Python -m pytest scripts/testing/tests/unit/test_cleanup_inventory.py scripts/testing/tests/unit/test_cleanup_semantics.py -q
```

Expected: collection fails because the cleanup modules do not exist.

- [ ] **Step 3: Implement immutable inventory records and semantic snapshotting**

```python
@dataclass(frozen=True, slots=True)
class InventoryRecord:
    source_root_id: str
    path: str
    tracked_status: str
    size_bytes: int
    sha256: str
    route: str
    test_case_id: str
    attempt_id: str
    terminal_status: str
    evidence_ids: tuple[str, ...]
    referenced_by_final_results: bool
    duplicate_group: str
    action: str
    destination: str
    reason: str

ALLOWED_ACTIONS = {
    "retain_active", "move_active", "archive_code",
    "archive_external", "remove_regenerable", "retain_ambiguous",
}
```

Classification order is: cited evidence, reproduction dependency, unique failure support, active import/command dependency, exact duplicate, proven cache/debris, then ambiguous retention.

`source_root_id` resolves through immutable inventory metadata containing the recovery evidence root, implementation root, their starting commits/status hashes, and the approved in-scope roots. Never place an unrestricted absolute source path directly into a removal command.

- [ ] **Step 4: Generate and validate the real baseline artifacts read-only**

```powershell
$RecoveryEvidenceRoot = 'C:\Users\Student\IBM-Granite-Edge-AI-Optimisation-using-TurboQuant\.worktrees\openvino-turboquant-recovery'
& $Python -m scripts.testing.tools.cleanup_inventory --source-root $RecoveryEvidenceRoot --canonical-root . --scope scripts/testing --scope experiments/raw-results --scope docs/testing/final-results --output-root docs/testing/cleanup --mode inventory
& $Python -m scripts.testing.tools.cleanup_semantics --repo-root . --output docs/testing/cleanup/baseline-semantic-snapshot.json
```

Assert the inventory metadata records the exact recovery worktree path, branch, HEAD, porcelain-v2 status hash, and approved scope roots. Assert the semantic snapshot records 169 outcomes, the 81/6/28/54 status split, 1,846 cited paths, six reports, 202 PDF pages, and the complete comparability matrix hash. Produce a separate delta report for recovery-source bytes that differ from the clean canonical worktree; classify every delta explicitly and do not treat it as scientific baseline drift.

- [ ] **Step 5: Run focused tests and commit**

```powershell
& $Python -m pytest scripts/testing/tests/unit/test_cleanup_inventory.py scripts/testing/tests/unit/test_cleanup_semantics.py -q
git add -- scripts/testing/tools scripts/testing/tests/unit docs/testing/cleanup
git commit -m "test: freeze testing cleanup baseline"
```

---

### Task 2: Move the final-results implementation into the reporting package

**Files:**
- Move: `scripts/testing/final_results/*` -> `scripts/testing/reporting/*`
- Modify: `scripts/testing/build_final_results.py`
- Modify: every tracked Python import of `scripts.testing.final_results`
- Modify: focused final-results tests that monkeypatch or import the old package
- Modify: generated provenance fields naming `scripts/testing/final_results/*`
- Modify: `docs/testing/cleanup/file-inventory.csv`

**Interfaces:**
- Replaces: `scripts.testing.final_results.*` with `scripts.testing.reporting.*`.
- Preserves: all public dataclasses, adapters, builders, renderers, validators, and comparison function signatures.
- Consumes: Task 1 baseline snapshot.

- [ ] **Step 1: Write import-boundary tests**

```python
def test_reporting_is_the_only_active_results_package():
    assert importlib.util.find_spec("scripts.testing.reporting.validate") is not None
    assert importlib.util.find_spec("scripts.testing.final_results") is None

def test_reporting_public_interfaces_are_stable():
    from scripts.testing.reporting.models import RouteBundle
    from scripts.testing.reporting.validate import validate_collection
    assert RouteBundle.__name__ == "RouteBundle"
    assert callable(validate_collection)
```

- [ ] **Step 2: Run the focused import tests to verify RED**

Run the new test plus the existing `test_final_results_models.py`, `test_final_results_csvio.py`, and `test_final_results_evidence.py`. Expected: the new package import fails.

- [ ] **Step 3: Perform Git-aware package moves and update imports**

Use `git mv scripts/testing/final_results scripts/testing/reporting`. Update Python imports, monkeypatch strings, JSON provenance sanitizer paths, PowerShell/Python command text, and tests. Do not add an old-package compatibility shim.

- [ ] **Step 4: Prove semantic identity**

Run all final-results model, adapter, renderer, comparison, and validator tests. Regenerate the Task 1 semantic snapshot to a temporary path and assert it equals the committed baseline snapshot byte-for-byte.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/testing/reporting scripts/testing/build_final_results.py scripts/testing/tests docs/testing/final-results docs/testing/cleanup/file-inventory.csv
git commit -m "refactor: move final results into reporting package"
```

---

### Task 3: Reorganize llama.cpp, AtomicBot, and animehacker campaign modules

**Files:**
- Create: `scripts/testing/campaigns/__init__.py`
- Create: `scripts/testing/campaigns/llama_cpp/__init__.py`
- Move: `scripts/testing/measure_llama_run.py` -> `scripts/testing/campaigns/llama_cpp/measure_run.py`
- Move: `scripts/testing/measure_llama_server.py` -> `scripts/testing/campaigns/llama_cpp/measure_server.py`
- Move: `scripts/testing/parse_llama_measurement.py` -> `scripts/testing/campaigns/llama_cpp/parse_measurement.py`
- Move: `scripts/testing/atomicbot/*` -> `scripts/testing/campaigns/atomicbot/*`
- Move: `scripts/testing/animehacker/*` -> `scripts/testing/campaigns/animehacker/*`
- Modify: all imports, subprocess paths, tests, and evidence provenance paths referencing the moved modules

**Interfaces:**
- Preserves: existing runner, matrix, state, safety, metric, and reconciliation callable signatures.
- Produces: route packages consumed by canonical CLI commands in Task 5.

- [ ] **Step 1: Write package and command-import tests**

Assert every moved module imports from its new path, old modules are absent, and importing any campaign module has no runtime side effect.

- [ ] **Step 2: Run route-focused tests to verify RED**

Run llama measurement/parsing tests, AtomicBot tests, and animehacker tests. Expected: new imports fail.

- [ ] **Step 3: Move modules and update imports mechanically**

Use `git mv` for tracked files. Update import paths and explicit script paths. Preserve module contents except for import resolution and package-relative resource paths.

- [ ] **Step 4: Run route regressions and semantic comparison**

Run all llama, AtomicBot, and animehacker unit/integration tests. Build the three route bundles read-only and compare them to Task 1 baseline entities and hashes.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/testing/campaigns scripts/testing/tests docs/testing/cleanup
git commit -m "refactor: organize llama atomicbot and animehacker campaigns"
```

---

### Task 4: Reorganize the OpenVINO campaign package

**Files:**
- Move: `scripts/testing/official_openvino/*` -> `scripts/testing/campaigns/openvino/*`
- Move: `scripts/testing/experimental_openvino_probe/*` -> `scripts/testing/campaigns/openvino/experimental_probe/*`
- Move: `scripts/testing/experimental_openvino_runner/*` -> `scripts/testing/campaigns/openvino/experimental_runner/*`
- Move: `scripts/testing/official_openvino_upstream_runner/*` -> `scripts/testing/campaigns/openvino/upstream_runner/*`
- Modify: all tracked OpenVINO imports, subprocess commands, specs, provenance files, and tests

**Interfaces:**
- Preserves: conversion, runner, metrics, quality, adaptive campaign, safety, acquisition, and reconciliation signatures.
- Produces: one `scripts.testing.campaigns.openvino` package consumed by Task 5.

- [ ] **Step 1: Write new-path and side-effect tests**

Assert all OpenVINO modules import under `scripts.testing.campaigns.openvino`, old package paths are absent, and import does not acquire models, convert artifacts, or start a runtime.

- [ ] **Step 2: Run OpenVINO tests to verify RED**

Run the conversion, runner, metrics, quality, campaign, reconciliation, acquisition, boundary, and expected-rejection tests. Expected: new-path assertions fail.

- [ ] **Step 3: Move modules and update every reference**

Use Git-aware moves. Update imports, monkeypatch strings, worker module invocations, source audit paths, and provenance records without altering campaign behavior.

- [ ] **Step 4: Run the complete OpenVINO safe test set**

Exclude only tests that would start external runtimes or enumerate unrelated Word processes. Include all deterministic campaign, adapter, comparison, evidence, and workbook-portability tests. Compare both OpenVINO route bundles to the Task 1 baseline.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/testing/campaigns/openvino scripts/testing/tests docs/testing/final-results docs/testing/cleanup
git commit -m "refactor: organize openvino testing campaigns"
```

---

### Task 5: Introduce the supported CLI surface

**Files:**
- Create: `scripts/testing/cli/__init__.py`
- Create: `scripts/testing/cli/run_llama.py`
- Create: `scripts/testing/cli/run_atomicbot.py`
- Create: `scripts/testing/cli/run_animehacker.py`
- Create: `scripts/testing/cli/run_openvino.py`
- Move: `scripts/testing/build_final_results.py` -> `scripts/testing/cli/build_results.py`
- Create: `scripts/testing/cli/validate_results.py`
- Move: `scripts/testing/Export-Final-Results-Pdf.ps1` -> `scripts/testing/cli/export_report.ps1`
- Create: `scripts/testing/tests/integration/test_testing_cli.py`
- Create: `scripts/testing/README.md`

**Interfaces:**
- Produces: `python -m scripts.testing.cli.<command>` entry points.
- Preserves: result-builder route choices and exit codes 0/1/2.
- Delegates: campaign logic to Task 3/4 packages; no duplicated runner logic.

- [ ] **Step 1: Write CLI contract tests**

```python
@pytest.mark.parametrize("module", [
    "run_llama", "run_atomicbot", "run_animehacker", "run_openvino",
    "build_results", "validate_results",
])
def test_supported_cli_help_is_side_effect_free(module):
    result = run_module(f"scripts.testing.cli.{module}", "--help")
    assert result.returncode == 0
    assert result.stderr == ""
```

Also assert documented route/mode choices, invalid-use exit 2, validation-failure exit 1, and that dispatch calls exactly one existing campaign function.

- [ ] **Step 2: Run CLI tests to verify RED**

Expected: CLI modules do not exist.

- [ ] **Step 3: Implement thin dispatchers**

Each route runner exposes `build_parser()` and `main(argv: Sequence[str] | None = None) -> int`. Mode choices map explicitly to moved command functions. `validate_results` calls the build-results parser with `--validate-only` behavior rather than duplicating validation logic.

- [ ] **Step 4: Update README and run CLI/integration tests**

Document supported commands, safety boundaries, mutating versus read-only behavior, and examples. Run every CLI with `--help` and run validate-results against the current final-results tree.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/testing/cli scripts/testing/README.md scripts/testing/tests/integration
git commit -m "feat: add supported testing command surface"
```

---

### Task 6: Move active tools and archive superseded scripts

**Files:**
- Move: active root maintenance scripts -> `scripts/testing/tools/`
- Move: superseded root scripts -> `archive/testing-code/2026-09-01/`
- Create: `archive/testing-code/README.md`
- Create: `archive/testing-code/MIGRATION.csv`
- Create: `scripts/testing/tests/unit/test_code_migration_inventory.py`
- Modify: `docs/testing/cleanup/file-inventory.csv`

**Interfaces:**
- Consumes: Task 1 file inventory and Tasks 2-5 supported import/CLI graph.
- Produces: complete old-path-to-supported-command mapping.
- Enforces: no unclassified root-level executable remains.

- [ ] **Step 1: Write migration completeness tests**

Assert every Task 1 root-level script is exactly one of: canonical CLI, active tool, campaign module, or archived code. Assert every archived row has old path, replacement or `none`, archive path, reason, and `d0eb34f2` as the last scientific baseline.

- [ ] **Step 2: Run tests to verify RED**

Expected: migration index and archive do not exist.

- [ ] **Step 3: Execute inventory-driven Git moves**

Use only rows classified `move_active` or `archive_code`. Active workbook, evidence, environment, audit, and controlled-register utilities move to `scripts/testing/tools`. Superseded scripts move intact to the code archive. Do not archive a script imported by supported code or referenced by the new README.

- [ ] **Step 4: Verify root cleanliness and import closure**

Assert `scripts/testing` root contains only `README.md`, `requirements.txt`, and the approved directories. Run an AST import scan and the supported CLI tests.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/testing archive/testing-code docs/testing/cleanup
git commit -m "refactor: separate active and historical testing tools"
```

---

### Task 7: Reorganize the test suite by responsibility

**Files:**
- Move: focused pure tests -> `scripts/testing/tests/unit/`
- Move: adapter/campaign/route-builder tests -> `scripts/testing/tests/integration/`
- Move: final route/release/PDF/workbook/CLI tests -> `scripts/testing/tests/acceptance/`
- Preserve: `scripts/testing/tests/fixtures/`
- Create: `docs/testing/cleanup/test-path-migration.csv`
- Modify: pytest path references in documentation and reproduction commands

**Interfaces:**
- Produces: deterministic test classification and old/new path index.
- Preserves: test names, assertions, fixtures, and collection count.

- [ ] **Step 1: Write test-layout validation**

Assert every tracked `test_*.py` appears once in `test-path-migration.csv`, every destination is one of unit/integration/acceptance, and pytest collection contains the same node IDs after replacing only file prefixes.

- [ ] **Step 2: Capture the pre-move collection manifest**

```powershell
& $Python -m pytest scripts/testing/tests --collect-only -q | Out-File -Encoding utf8 docs/testing/cleanup/pre-move-pytest-collection.txt
```

- [ ] **Step 3: Move tests using the checked-in classification map**

Use `git mv`; do not rewrite test behavior. Update relative fixture paths, monkeypatch module strings already changed by Tasks 2-6, and documented test commands.

- [ ] **Step 4: Compare collection and run all safe tests**

Write the post-move collection manifest and assert the normalized node-ID set equals the pre-move set. Run unit, integration, then acceptance suites separately.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/testing/tests docs/testing/cleanup docs/testing/final-results
git commit -m "test: organize testing verification by responsibility"
```

---

### Task 8: Build the common published-route layout migrator

**Files:**
- Create: `scripts/testing/reporting/layout.py`
- Create: `scripts/testing/tests/unit/test_final_results_layout.py`
- Create: `docs/testing/cleanup/PATH-MIGRATION.csv`
- Modify: `scripts/testing/reporting/validate.py`
- Modify: `scripts/testing/cli/build_results.py`

**Interfaces:**
- Produces: `plan_route_migration(route_root: Path) -> tuple[PathMove, ...]`.
- Produces: `consolidate_validation(route_root: Path) -> dict[str, object]`.
- Produces: `validate_layout(route_root: Path) -> tuple[str, ...]`.
- Enforces: README, reports, data, evidence, validation, and reproduction boundaries.

- [ ] **Step 1: Write miniature old-layout migration tests**

Create a fixture containing workbook/generated, results, quality, failures, validation receipts, and reproduction fragments. Assert the migration plan is collision-free, lossless, deterministic, and produces exactly the six-part target layout.

- [ ] **Step 2: Run tests to verify RED**

Expected: layout module does not exist.

- [ ] **Step 3: Implement declarative path moves and validation consolidation**

`PathMove` contains `old_path`, `new_path`, `sha256`, `role`, and `reason`. Consolidated `validation.json` stores named prior receipts under `checks`, preserves every finding/limitation, and computes overall validity. `validation.md` is rendered from the same object.

- [ ] **Step 4: Add stale-path and collision tests**

Assert duplicate destinations, missing source files, unknown roles, stale removed paths, manifest self-inclusion, and validation disagreement all fail closed.

- [ ] **Step 5: Commit**

```powershell
git add -- scripts/testing/reporting/layout.py scripts/testing/reporting/validate.py scripts/testing/cli/build_results.py scripts/testing/tests/unit/test_final_results_layout.py docs/testing/cleanup/PATH-MIGRATION.csv
git commit -m "feat: define compact final results layout"
```

---

### Task 9: Migrate the three llama.cpp-family result routes

**Files:**
- Move/Modify: `docs/testing/final-results/01-upstream-llama-cpp/**`
- Move/Modify: `docs/testing/final-results/02-atomicbot-turboquant/**`
- Move/Modify: `docs/testing/final-results/03-animehacker-tq3-0/**`
- Modify: `scripts/testing/reporting/llama_adapter.py`
- Modify: route acceptance tests
- Modify: `docs/testing/cleanup/PATH-MIGRATION.csv`

**Interfaces:**
- Consumes: Task 8 layout migrator.
- Preserves: all three `RouteBundle` entities and report bytes unless an embedded path requires regeneration.

- [ ] **Step 1: Add old-versus-new route assertions**

Capture the three baseline route bundles and report hashes from Task 1. Tests assert identical entity serialization, status counts, quality boundaries, failure/deviation rows, claim mappings, and evidence content hashes after migration.

- [ ] **Step 2: Run migration acceptance tests to verify RED**

Expected: current folders violate the new layout contract.

- [ ] **Step 3: Apply the migration plan with Git-aware moves**

Move reports, canonical tables, evidence, consolidated validation, and consolidated reproduction files. Update adapter output paths and report links. Keep every source record; omit only non-applicable empty placeholders.

- [ ] **Step 4: Regenerate manifests and validate the three routes**

Run route builders against a disposable output root, compare to migrated committed routes, verify Markdown/DOCX parity, and inspect unchanged PDFs structurally.

- [ ] **Step 5: Commit**

```powershell
git add -- docs/testing/final-results/01-upstream-llama-cpp docs/testing/final-results/02-atomicbot-turboquant docs/testing/final-results/03-animehacker-tq3-0 scripts/testing/reporting/llama_adapter.py scripts/testing/tests docs/testing/cleanup/PATH-MIGRATION.csv
git commit -m "refactor: standardize llama family result routes"
```

---

### Task 10: Migrate the OpenVINO and cross-route result routes

**Files:**
- Move/Modify: `docs/testing/final-results/04-openvino-experimental-fork/**`
- Move/Modify: `docs/testing/final-results/05-openvino-official-upstream/**`
- Move/Modify: `docs/testing/final-results/06-cross-route-comparison/**`
- Modify: `scripts/testing/reporting/openvino_adapter.py`
- Modify: `scripts/testing/reporting/openvino_report.py`
- Modify: `scripts/testing/reporting/comparison.py`
- Modify: route and workbook-portability acceptance tests
- Modify: `docs/testing/cleanup/PATH-MIGRATION.csv`

**Interfaces:**
- Consumes: Task 8 layout migrator.
- Preserves: 27/54 experimental outcomes, 15/5/25 official outcomes, 15 matched direct-comparison cases, and all quality-method boundaries.

- [ ] **Step 1: Add OpenVINO semantic and portability assertions**

Assert source XLSX hashes remain unchanged and evidence-only, portable derivatives have zero absolute paths, route bundles equal baseline serialization, and comparison decisions equal the Task 1 snapshot.

- [ ] **Step 2: Run migration acceptance tests to verify RED**

Expected: current folders violate the new layout contract.

- [ ] **Step 3: Apply Git-aware route moves and update generators**

Move portable XLSX files to `reports`, originals to evidence-only locations, canonical tables to `data`, and consolidate validation/reproduction. Update route manifests after all paths settle.

- [ ] **Step 4: Validate reports, workbooks, and comparison**

Run both OpenVINO route tests, workbook portability tests, comparison guards, Markdown/DOCX parity, PDF structural checks, and field-by-field semantic comparison.

- [ ] **Step 5: Commit**

```powershell
git add -- docs/testing/final-results/04-openvino-experimental-fork docs/testing/final-results/05-openvino-official-upstream docs/testing/final-results/06-cross-route-comparison scripts/testing/reporting scripts/testing/tests docs/testing/cleanup/PATH-MIGRATION.csv
git commit -m "refactor: standardize openvino and comparison routes"
```

---

### Task 11: Refresh the collection portal, catalog, metadata, and reproduction guide

**Files:**
- Modify: `docs/testing/final-results/README.md`
- Modify: `docs/testing/final-results/REPRODUCING.md`
- Modify: `docs/testing/final-results/CHANGELOG.md`
- Modify: `docs/testing/final-results/LICENSES.md`
- Modify: `docs/testing/final-results/ro-crate-metadata.json`
- Modify: `docs/testing/final-results/manifest-sha256.txt`
- Modify: `docs/testing/final-results/catalog/*`
- Modify: `docs/testing/final-results/validation/*`
- Modify: release acceptance tests

**Interfaces:**
- Consumes: Tasks 2-10 new paths and supported CLI.
- Produces: navigable collection root with no stale old-path references.

- [ ] **Step 1: Write portal and metadata migration tests**

Assert every route links README/reports/data/evidence/validation/reproduction; every RO-Crate file entity uses a new relative path; every reproduction command uses `scripts.testing.cli`; top manifest is sorted and self-excluding; and `PATH-MIGRATION.csv` covers every removed published path.

- [ ] **Step 2: Run release tests to verify RED**

Expected: portal, metadata, and commands still contain old paths.

- [ ] **Step 3: Rewrite collection-level surfaces**

Describe the common layout once, update the no-rerun boundary, record the cleanup version in CHANGELOG, preserve license/authorship limitations, rebuild RO-Crate activities, and rebuild catalogs from canonical route data.

- [ ] **Step 4: Build the top manifest last and validate metadata**

Use canonical staged Git bytes and the scoped LF/binary attributes. Validate exact file set, hashes, provenance, portal links, and clean reproduction commands.

- [ ] **Step 5: Commit**

```powershell
git add -- docs/testing/final-results scripts/testing/tests/acceptance docs/testing/cleanup/PATH-MIGRATION.csv
git commit -m "docs: refresh compact testing results portal"
```

---

### Task 12: Implement and execute the external historical archive copy

**Files:**
- Create: `scripts/testing/tools/archive_transaction.py`
- Create: `scripts/testing/tests/unit/test_archive_transaction.py`
- Create: `docs/testing/cleanup/archive-plan.csv`
- Create: `docs/testing/cleanup/archive-summary.json`
- External create: `C:\Users\Student\Downloads\Granite-Testing-Historical-Archive-2026-09-01*`

**Interfaces:**
- Produces: `ArchiveEntry`, `plan_archive(records)`, `copy_archive(plan, destination)`, and `verify_archive(plan, destination)`.
- Produces: journal states `planned`, `copied`, `verified`, `migrated`, and `removed`.
- Resolves archive sources from the immutable `source_root_id` metadata captured in Task 1 and revalidates the recovery worktree identity before every operation.
- This task copies and verifies; it does not remove any source.

- [ ] **Step 1: Write transactional archive tests**

```python
def test_source_is_never_removed_by_copy_or_verify(tmp_path):
    source, destination, plan = make_archive_fixture(tmp_path)
    copy_archive(plan, destination)
    assert source.exists()
    assert verify_archive(plan, destination).valid
    assert source.exists()
```

Also test insufficient space, destination collision, interrupted resume, long paths, hash mismatch, symlink/reparse rejection, and path escape rejection.

- [ ] **Step 2: Run tests to verify RED**

Expected: archive transaction module does not exist.

- [ ] **Step 3: Implement exact-path copy and verification**

Resolve every source beneath an approved root and every destination beneath the selected archive root. Refuse unresolved paths, reparse points, duplicate destinations, existing different bytes, and any copy lacking size/SHA equality.

- [ ] **Step 4: Generate the real plan, verify disk capacity, copy, and verify**

```powershell
& $Python -m scripts.testing.tools.archive_transaction plan --inventory docs/testing/cleanup/file-inventory.csv --output docs/testing/cleanup/archive-plan.csv
& $Python -m scripts.testing.tools.archive_transaction copy --plan docs/testing/cleanup/archive-plan.csv --destination $ArchiveRoot
& $Python -m scripts.testing.tools.archive_transaction verify --plan docs/testing/cleanup/archive-plan.csv --destination $ArchiveRoot --receipt "$ArchiveRoot\archive-receipt.json"
```

Require zero unverified rows before proceeding.

- [ ] **Step 5: Commit repository-side tooling and receipts**

```powershell
git add -- scripts/testing/tools/archive_transaction.py scripts/testing/tests/unit/test_archive_transaction.py docs/testing/cleanup/archive-plan.csv docs/testing/cleanup/archive-summary.json
git commit -m "feat: create verified testing history archive"
```

---

### Task 13: Migrate retained raw evidence into the active route structure

**Files:**
- Move/Modify: retained tracked files below `experiments/raw-results/**`
- Copy/Add: retained modified, untracked, or ignored source bytes from the read-only recovery evidence worktree when their inventory action is `retain_active` or `move_active`
- Create: `experiments/raw-results/README.md`
- Create: `experiments/raw-results/evidence-manifest.csv`
- Create: `experiments/raw-results/failure-records/**`
- Modify: all route evidence indexes and reproduction references
- Modify: `docs/testing/cleanup/PATH-MIGRATION.csv`
- Create: `scripts/testing/tests/integration/test_retained_evidence_migration.py`

**Interfaces:**
- Consumes: Task 1 classification and Task 12 verified archive receipt.
- Produces: `experiments/raw-results/retained/<route>` with every active evidence path.
- Preserves: evidence IDs and content hashes; only relative paths may change.
- Hydrates: non-canonical retained bytes into the implementation worktree by exact-path, size, and SHA-256 verification; it never edits or moves their recovery-worktree sources.

- [ ] **Step 1: Write complete evidence-closure tests**

Assert every cited path maps to exactly one retained destination, every destination SHA/size matches baseline, every unique failed/blocked/unavailable support artifact remains retained, and no `archive_external` row enters the retained tree.

- [ ] **Step 2: Run migration tests to verify RED**

Expected: retained route tree and manifest do not exist.

- [ ] **Step 3: Apply inventory-driven Git moves and update references**

Use `git mv` for tracked sources already present with matching bytes in the implementation worktree. For retained modified, untracked, or ignored recovery-source files, copy each exact file into a collision-free destination in the implementation worktree, verify size and SHA-256 before staging, and record the source/destination pair in `PATH-MIGRATION.csv`. If a recovery-source file would overwrite different canonical bytes, stop and require an explicit inventory destination rather than choosing a winner. Copy no source bytes through a renderer or serializer. Update evidence index paths, source labels only where the path label is explicitly structural, reproduction references, and path migration rows.

- [ ] **Step 4: Validate 1,846 evidence relationships and route semantics**

Run evidence, route, comparison, failure, and clean-path validation. Require zero missing/hash/size/unsafe/identity findings and exact Task 1 semantic snapshot equality. Assert every `retain_active` and `move_active` inventory row exists as a tracked implementation-worktree file with the recorded bytes so a clean Git archive needs no later hydration.

- [ ] **Step 5: Commit**

```powershell
git add -- experiments/raw-results docs/testing/final-results docs/testing/cleanup/PATH-MIGRATION.csv scripts/testing/tests/integration/test_retained_evidence_migration.py
git commit -m "refactor: retain canonical testing evidence by route"
```

---

### Task 14: Remove only verified archived sources and regenerable debris

**Files:**
- Modify: `docs/testing/cleanup/file-inventory.csv`
- Create: `docs/testing/cleanup/removal-receipt.json`
- Create: `scripts/testing/tests/acceptance/test_cleanup_removal_receipt.py`
- Remove: only exact inventory paths marked `archive_external` with verified archive receipt, or `remove_regenerable` with independent classification

**Interfaces:**
- Consumes: Task 12 valid archive receipt and Task 13 migrated-path validation.
- Produces: exact removal receipt containing original path, action, archive destination or regenerable rule, original SHA/size, and removal timestamp.
- Refuses: any unverified, ambiguous, cited, imported, or out-of-root path.
- Restricts: source removal to exact in-scope files under the inventory row's approved recovery or implementation root whose identity still matches Task 1; never removes a directory or any unrelated worktree path.

- [ ] **Step 1: Write fail-closed removal tests**

Assert removal refuses an unverified archive row, mismatched destination, cited file, imported script, ambiguous record, path escape, root directory, reparse point, and wildcard. Assert a verified fixture removes only the exact listed file.

- [ ] **Step 2: Run tests to verify RED**

Expected: removal mode and receipt do not exist.

- [ ] **Step 3: Implement `remove-verified` mode**

Require archive receipt SHA, inventory SHA, source-root identity, destination verification, source SHA/size, allowed root, and exact action. Before each file, resolve its relative path beneath the row's recorded recovery or implementation root and recheck that root without following reparse points. Recovery-worktree rows may be removed only when they are in scope and classified `archive_external` or `remove_regenerable`; protected, retained, ambiguous, and unrelated dirty/untracked rows remain untouched. Process files individually; never accept a directory as a recursive removal target.

- [ ] **Step 4: Run dry-run, review exact inventory, then remove**

```powershell
& $Python -m scripts.testing.tools.archive_transaction remove-verified --inventory docs/testing/cleanup/file-inventory.csv --archive-receipt "$ArchiveRoot\archive-receipt.json" --output docs/testing/cleanup/removal-receipt.json --dry-run
& $Python -m scripts.testing.tools.archive_transaction remove-verified --inventory docs/testing/cleanup/file-inventory.csv --archive-receipt "$ArchiveRoot\archive-receipt.json" --output docs/testing/cleanup/removal-receipt.json
```

Verify the receipt against Git status and filesystem inventory. Stop on the first mismatch.

- [ ] **Step 5: Run evidence closure and commit tracked removals/receipt**

```powershell
& $Python -m pytest scripts/testing/tests/acceptance/test_cleanup_removal_receipt.py scripts/testing/tests/integration/test_retained_evidence_migration.py -q
git add -u -- scripts/testing experiments/raw-results docs/testing/final-results
git add -- docs/testing/cleanup/file-inventory.csv docs/testing/cleanup/removal-receipt.json scripts/testing/tests/acceptance/test_cleanup_removal_receipt.py
git commit -m "chore: remove verified archived testing debris"
```

---

### Task 15: Produce the final cleanup report and run release validation

**Files:**
- Create: `docs/testing/cleanup/README.md`
- Create: `docs/testing/cleanup/before-after-summary.md`
- Create: `docs/testing/cleanup/final-semantic-snapshot.json`
- Create: `docs/testing/cleanup/clean-archive-validation.json`
- Modify: `docs/testing/final-results/validation/validation.json`
- Modify: `docs/testing/final-results/validation/validation.md`
- Modify: `docs/testing/final-results/manifest-sha256.txt`
- Modify: `docs/testing/final-results/ro-crate-metadata.json`
- Modify: `docs/testing/final-results/CHANGELOG.md`

**Interfaces:**
- Consumes: all prior task artifacts and receipts.
- Produces: final semantic equality receipt, file/size reduction report, archive identity, and clean-archive validation.

- [ ] **Step 1: Write final acceptance assertions**

Assert exact baseline/final semantic snapshot equality, 169 and 81/6/28/54 totals, 1,846 evidence relationships, six parity pairs, 202 PDF pages, two zero-absolute-path portable XLSX files, unchanged comparison decisions, complete migration indexes, valid archive/removal receipts, and no stale old path.

- [ ] **Step 2: Run the complete safe test suite**

Run unit, integration, and acceptance groups from separate fresh worktree/archive copies where tests mutate generated outputs. Exclude only tests that would enumerate or manipulate unrelated user processes; cover unchanged PDF/export code through existing reviewed evidence and safe structural tests.

- [ ] **Step 3: Validate an untouched Git archive**

Create a short-path disposable archive from HEAD with no evidence hydration. Run supported `validate_results` and require all release gates to exit 0. Store the machine receipt in `clean-archive-validation.json`.

- [ ] **Step 4: Perform final visual/data QA and write before/after report**

Render all six PDFs page by page with PyMuPDF, inspect all 202 pages, open both portable XLSX files read-only with openpyxl, and report exact before/after tracked/untracked/ignored file counts and bytes. Do not claim size reduction from externally archived files until the removal receipt verifies them.

- [ ] **Step 5: Build manifests last, verify scoped status, and commit**

```powershell
git add -- docs/testing/cleanup docs/testing/final-results
git diff --cached --check
git commit -m "docs: finalize cleaned testing results library"
```

After this commit, request a broad whole-branch review covering every cleanup commit, every parked/minor finding, the external archive receipt, and the untouched canonical Git archive.
