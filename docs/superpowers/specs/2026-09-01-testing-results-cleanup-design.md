# Testing and Results Repository Cleanup Design

**Date:** 2026-09-01  
**Status:** Approved design, pending implementation plan  
**Baseline commit:** `d0eb34f2fca9e1bc00e176f325195e9b7b663a18`  
**Affected areas:** `scripts/testing`, `experiments/raw-results`, and `docs/testing/final-results`

## 1. Purpose

The repository contains a validated five-route testing campaign and a cross-route results library, but its active testing surface is difficult to navigate. Root-level scripts mix supported commands with one-off utilities, raw-results contains authoritative evidence beside abandoned or superseded outputs, and published route folders expose implementation-oriented nesting rather than a consistent reader-oriented structure.

This cleanup will create one understandable lifecycle:

```text
supported testing code -> retained evidence -> validated published results
```

The cleanup is structural. It must not rerun benchmarks, change scientific observations, recalculate quality under a new method, hide failed attempts, or weaken the evidence chain.

## 2. Approved approach

Use an evidence-driven canonical core:

- Keep only supported testing and reporting commands in the active code tree.
- Preserve proven implementation modules and place them behind a small documented CLI surface.
- Move superseded code outside the active testing tree and document every replacement.
- Retain every raw artifact cited by the published library and every unique artifact needed to explain a failed, blocked, or unavailable result.
- Copy unreferenced historical raw material to a verified external archive before removing it from the active repository.
- Give all six published result routes the same compact six-part layout.
- Preserve stable IDs, values, statuses, evidence hashes, reports, and comparison boundaries.

## 3. Scope

### 3.1 In scope

- Inventorying all tracked, untracked, and ignored files below the three affected roots.
- Classifying code and data as active, retained historical, externally archived, duplicated, regenerable, or ambiguous.
- Reorganizing supported testing code and tests.
- Archiving superseded scripts with a migration index.
- Reorganizing retained raw evidence by canonical route.
- Externally archiving unreferenced raw attempts and superseded generated material.
- Removing regenerable caches and disposable test outputs.
- Reorganizing the six published result routes.
- Consolidating fragmented validation and reproduction documents.
- Updating imports, commands, evidence paths, manifests, RO-Crate metadata, portal links, and release receipts.
- Verifying semantic equivalence and clean-archive reproducibility.

### 3.2 Out of scope

- Rerunning any model benchmark.
- Rerunning or changing quality adjudication.
- Changing prompt suites, rubrics, weights, caps, denominators, or aggregations.
- Converting a failed, blocked, unavailable, or historical result into a pass or zero.
- Altering raw evidence contents.
- Replacing the proven campaign implementations with a new testing framework.
- Merging or pushing without a separate user decision after verification.

## 4. Baseline invariants

The cleanup must preserve the following validated baseline:

- Five repository routes plus one cross-route comparison.
- 169 planned outcomes: 81 passed, 6 failed, 28 blocked, and 54 artifact-unavailable.
- 1,846 unique cited evidence paths with valid path, SHA-256, and size relationships.
- Six Markdown/DOCX report pairs with exact structural parity.
- Six PDFs containing 202 searchable, nonblank pages.
- Two original OpenVINO XLSX files retained as nonportable source evidence.
- Two portable OpenVINO XLSX derivatives containing no machine-specific absolute paths.
- Direct comparison confined to the 15 matched OpenVINO cases.
- Legacy llama quality and OpenVINO-v3 quality remain methodologically distinct.
- No universal repository ranking or incompatible quality ranking.
- All thirteen release-validation gates pass on an untouched canonical Git archive.

Any drift in these invariants blocks the cleanup.

## 5. Workspace and preservation boundary

Implementation will use a new linked worktree and branch based on `d0eb34f2`. The current recovery worktree remains a read-only evidence source until every dirty, untracked, or ignored file in scope has been classified.

The implementation must:

- Capture the starting branch, commit, status, sizes, and hashes.
- Preserve unrelated worktree changes.
- Never use `git reset --hard`, `git clean`, blanket checkout restoration, or wildcard deletion.
- Never overwrite the protected dirty AtomicBot route manifest or DOCX, or the current animehacker manifest materialization.
- Never touch or enumerate unrelated Word processes.
- Treat any ambiguous file as retained until independently resolved.

## 6. Target code structure

```text
scripts/testing/
├── README.md
├── requirements.txt
├── cli/
│   ├── run_llama.py
│   ├── run_atomicbot.py
│   ├── run_animehacker.py
│   ├── run_openvino.py
│   ├── build_results.py
│   ├── validate_results.py
│   └── export_report.ps1
├── campaigns/
│   ├── llama_cpp/
│   ├── atomicbot/
│   ├── animehacker/
│   └── openvino/
├── reporting/
├── tools/
└── tests/
    ├── unit/
    ├── integration/
    ├── acceptance/
    └── fixtures/
```

### 6.1 Responsibilities

- `cli`: supported contributor-facing entry points only.
- `campaigns`: model execution, measurement, scoring, safety, state, and reconciliation logic.
- `reporting`: canonical models, route adapters, renderers, comparison logic, workbook portability, and validators.
- `tools`: workbook control, evidence hashing, environment capture, audits, and narrowly scoped maintenance utilities.
- `tests/unit`: isolated parsing, schema, metric, and boundary tests.
- `tests/integration`: adapters, route builders, evidence relationships, and renderer integration.
- `tests/acceptance`: route, release, PDF, workbook, CLI, and clean-archive acceptance tests.
- `tests/fixtures`: bounded controlled inputs only; no full raw campaign copies.

### 6.2 CLI design

The CLI layer stays thin and delegates to existing proven modules. It does not duplicate campaign logic. Each command must support `--help`, return documented exit codes, and distinguish read-only validation from mutating generation.

Canonical workflow families are:

- Route-specific campaign execution: llama.cpp, AtomicBot, animehacker, and OpenVINO.
- Final-results build.
- Final-results validation.
- Owned report export.

Reproduction documents will reference only these supported commands.

### 6.3 Superseded code

Superseded scripts move out of the active tree to:

```text
archive/testing-code/2026-09-01/
```

The archive will contain `MIGRATION.csv` with:

```text
old_path,new_supported_command,archive_path,reason,last_verified_commit
```

The active root will not retain dozens of compatibility wrappers. Git history remains the complete historical source record.

## 7. Target raw-evidence structure

```text
experiments/raw-results/
├── README.md
├── retained/
│   ├── upstream-llama-cpp/
│   ├── atomicbot-turboquant/
│   ├── animehacker-tq3-0/
│   ├── openvino-experimental-fork/
│   └── openvino-official-upstream/
├── failure-records/
└── evidence-manifest.csv
```

### 7.1 Retain in the repository

- Every artifact cited by a final evidence index.
- Every unique log or receipt required to substantiate a failed, blocked, unavailable, or historical result.
- Inputs required by supported reproduction commands.
- Authoritative quality outputs and adjudications.
- Campaign specifications, environment records, state, and reconciliation receipts used by the final library.
- Controlled testing registers.
- Original source workbooks and portable derivatives.

### 7.2 External historical archive

The default location is:

```text
C:\Users\Student\Downloads\Granite-Testing-Historical-Archive-2026-09-01
```

Its structure is:

```text
Granite-Testing-Historical-Archive-2026-09-01/
├── README.md
├── raw-results/
├── superseded-generated-results/
├── archive-manifest.csv
├── duplicate-groups.csv
└── archive-receipt.json
```

Archive candidates include:

- Unreferenced abandoned attempts.
- Superseded controller outputs.
- Duplicate run directories not selected as authoritative.
- Intermediate generated workbooks.
- Obsolete diagnostics that support no published status.
- Historical scripts' output folders.

The archive preserves original relative paths. It is created collision-safely and never overwrites a different existing file.

### 7.3 Remove without archiving

Only proven regenerable debris may be removed directly:

- `__pycache__`.
- `.pytest_cache`.
- Temporary page renders.
- Temporary JUnit files.
- Empty locks belonging to completed processes.
- Disposable test outputs generated by the reporting suite.

No failed attempt is disposable merely because it failed.

## 8. File classification model

The cleanup inventory contains one row per file with:

```text
path
tracked_status
size_bytes
sha256
route
test_case_id
attempt_id
terminal_status
evidence_ids
referenced_by_final_results
duplicate_group
proposed_action
destination
reason
```

Allowed actions are:

- `retain_active`
- `move_active`
- `archive_code`
- `archive_external`
- `remove_regenerable`
- `retain_ambiguous`

Classification precedence is evidence reference, reproduction dependency, unique failure support, active import/command dependency, duplicate status, then historical value. An earlier rule always overrides a later cleanup opportunity.

## 9. Target published-results structure

Each of the six routes uses:

```text
<route>/
├── README.md
├── reports/
│   ├── <route>-report.md
│   ├── <route>-report.docx
│   ├── <route>-report.pdf
│   └── <route>-results.xlsx
├── data/
│   ├── route.json
│   ├── attempts.csv
│   ├── measurements.csv
│   ├── summaries.csv
│   ├── quality.csv
│   ├── failures.csv
│   └── deviations.csv
├── evidence/
│   ├── evidence-index.csv
│   ├── claim-evidence-map.csv
│   └── manifest-sha256.txt
├── validation/
│   ├── validation.json
│   └── validation.md
└── reproduction/
    ├── README.md
    └── environment.json
```

Files not applicable to a route are omitted rather than represented by empty placeholders. OpenVINO routes publish portable XLSX derivatives under `reports`; original nonportable XLSX inputs remain evidence-only.

Repository-level `catalog`, `standards`, and `validation` remain separate. The portal links every route and describes the common layout once.

## 10. Published-results migration rules

- Stable IDs cannot change.
- Scientific values, statuses, prompt scores, quality dimensions, aggregates, and failure classifications cannot change.
- Failed, blocked, unavailable, and historical records remain present.
- Evidence content hashes remain unchanged when only paths move.
- Original source workbook bytes remain unchanged.
- Existing DOCX and PDF bytes remain unchanged unless an embedded stale path makes regeneration necessary.
- Route manifests and the top manifest are rebuilt only after path migration is complete.
- RO-Crate entities and generation activities are updated to the new relative paths.
- `PATH-MIGRATION.csv` records every published old path and new path.
- The validator rejects stale references to removed paths.
- Reproduction documents use supported CLI commands only.

## 11. Transactional archive and cleanup sequence

1. Verify available disk space against the proposed external archive plus safety margin.
2. Generate the complete inventory and proposed-action report.
3. Stop if any in-scope file remains ambiguous without an explicit retain decision.
4. Copy external-archive candidates without changing their sources.
5. Rehash and resize-check every copied file.
6. Write and validate the archive manifest and receipt.
7. Move retained tracked evidence with Git-aware renames.
8. Rewrite evidence paths without changing evidence IDs or content hashes.
9. Perform old-versus-new semantic reconciliation.
10. Remove only source copies with verified archive receipts.
11. Remove regenerable debris only from exact inventory paths.
12. Run complete validation against the active worktree and a clean Git archive.

The transaction journal records `planned`, `copied`, `verified`, `migrated`, and `removed` states. An interrupted run resumes from the journal and never assumes an attempted operation succeeded.

## 12. Error handling and rollback

- Insufficient disk space: stop before copying.
- Copy or hash mismatch: retain the source and mark the archive incomplete.
- Ambiguous file: retain it.
- Broken import or command: block the migration until the supported path works.
- Changed value, status, ID, or comparison classification: block the cleanup.
- Missing evidence after a move: restore the old path or correct the migration before proceeding.
- Failed clean-archive validation: do not merge.
- External archive exists with conflicting bytes: choose a numbered sibling; never overwrite.

Tracked changes remain recoverable through Git. Externally archived files retain original paths and hashes. No archived-source removal occurs before both recovery mechanisms are verified; untracked regenerable debris is removed only after its classification and exact path are independently checked.

## 13. Implementation stages

1. Inventory and classification only.
2. Testing-code restructure and CLI migration.
3. Published-results restructure.
4. External historical archive creation and verification.
5. Active retained-evidence migration.
6. Exact cleanup of receipted sources and regenerable debris.
7. Final validation, independent review, and handoff.

Each stage uses focused tests, a scoped commit, and an independent review before the next stage begins.

## 14. Acceptance criteria

The cleanup is acceptable only when:

1. The active testing root contains documented supported commands and clear responsibility-based packages.
2. Every old supported command has a documented new command.
3. Every superseded code path appears in `MIGRATION.csv` or Git history with an explicit reason.
4. Every externally archived file has matching path, size, and SHA-256 in the archive receipt.
5. Every removed tracked or untracked source was either externally archived or proven regenerable.
6. The 169-attempt totals remain 81 passed, 6 failed, 28 blocked, and 54 artifact-unavailable.
7. All 1,846 retained evidence relationships resolve and validate.
8. Failure, blocked, unavailable, deviation, and historical records remain visible.
9. All six Markdown/DOCX report pairs retain parity.
10. All 202 PDF pages remain searchable, nonblank, and unclipped.
11. Portable OpenVINO XLSX files contain zero machine-specific absolute paths.
12. Cross-route comparability decisions and quality-method boundaries remain unchanged.
13. Old and new canonical datasets compare field by field with no semantic drift.
14. All manifests, metadata, portal links, and reproduction commands use the new paths.
15. The full safe regression suite passes.
16. An untouched canonical Git archive passes every release gate without external evidence hydration.
17. No unrelated dirty or untracked work is staged, overwritten, or discarded.
18. An independent final review reports no load-bearing findings.

## 15. Deliverables

- Clean active testing-code tree.
- Supported CLI reference in `scripts/testing/README.md`.
- Code migration archive and `MIGRATION.csv`.
- Raw-results inventory and repository evidence manifest.
- Verified external historical archive and receipt.
- Six consistently structured published result routes.
- Repository-wide `PATH-MIGRATION.csv`.
- Updated portal, reproduction guide, manifests, RO-Crate, and readiness receipts.
- Before/after inventory, size, file-count, and semantic-equivalence report.
- Final clean-archive validation receipt.
