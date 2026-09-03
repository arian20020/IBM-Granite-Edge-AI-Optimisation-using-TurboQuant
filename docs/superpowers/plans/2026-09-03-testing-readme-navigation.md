# Beginner-Friendly Testing README Hierarchy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every meaningful testing directory a complete, beginner-friendly README that explains how to navigate from the testing roots to the individual files.

**Architecture:** Use a progressive documentation hierarchy. Each README explains its own folder, immediate child folders, immediate files, place in the evidence flow, authority boundaries and next navigation step. Curated and semantic folders receive READMEs; generated run, attempt, timestamp, sample, warm-up, pilot and cache folders are documented by their parent instead of being modified.

**Tech Stack:** Markdown, Git, PowerShell inventory checks, repository-relative links

**Spec:** `docs/superpowers/specs/2026-09-03-testing-readme-navigation-design.md`

## Global Constraints

- Work only in `C:\Users\Student\Granite-Main-Merge-2026-09-02` on branch `main`.
- Change only `README.md` files plus this approved plan and its design document.
- Preserve useful existing README content, exact campaign totals, status terms, release IDs and safety warnings.
- Use simple English, but impose no word limit; completeness controls length.
- Explain every immediate file individually unless it belongs to a homogeneous generated group that is fully explained by name pattern and format.
- Do not add READMEs inside run-ID, attempt-ID, date, timestamp, `sample-*`, `warmup`, `pilot`, `__pycache__`, build, model or copied third-party directories.
- Do not alter scripts, test data, logs, workbooks, reports, manifests or generated artefacts.
- Treat raw evidence as immutable and never imply that documentation proves a command ran.
- Distinguish source inputs, raw evidence, processed results, generated reports and validation receipts.
- Distinguish passed, failed, blocked and unavailable results; never turn missing evidence into zero.
- Keep cross-route comparison claims within the published comparability rules.

---

### Task 1: Strengthen the three navigation roots

**Files:**

- Modify: `docs/testing/README.md`
- Modify: `scripts/testing/README.md`
- Modify: `experiments/README.md`

**Interfaces:**

- Consumes: the approved design, current directory tree and existing root guides.
- Produces: the three entry points used by every later README batch.

- [ ] **Step 1: Capture the clean baseline and immediate contents**

Run:

```powershell
git status --short --branch
Get-ChildItem docs/testing -Force | Select-Object Name, Mode
Get-ChildItem scripts/testing -Force | Select-Object Name, Mode
Get-ChildItem experiments -Force | Select-Object Name, Mode
```

Expected: branch `main` is clean apart from the committed design/plan history; the three roots exist.

- [ ] **Step 2: Read every existing root guide and the canonical release guide**

Run:

```powershell
Get-Content -Raw docs/testing/README.md
Get-Content -Raw scripts/testing/README.md
Get-Content -Raw experiments/README.md
Get-Content -Raw docs/testing/final-results/README.md
```

Preserve all existing campaign controls and command safety boundaries.

- [ ] **Step 3: Expand each root README**

For each root, add:

- a beginner “Start here” route;
- an evidence-flow explanation;
- a table of every immediate child folder;
- a table of every immediate file and its role;
- guidance for finding results, commands, raw evidence, failures and reports;
- clear editing and authority boundaries; and
- links to the other two roots where the workflow crosses between them.

- [ ] **Step 4: Verify root coverage and links**

Run:

```powershell
git diff --check
Select-String -Path docs/testing/README.md,scripts/testing/README.md,experiments/README.md -Pattern '^## Start here','^## Folders','^## Files'
```

Expected: no whitespace errors; each guide contains beginner navigation and inventory sections appropriate to its contents.

- [ ] **Step 5: Commit the root navigation**

```powershell
git add docs/testing/README.md scripts/testing/README.md experiments/README.md
git commit -m "docs(testing): expand beginner navigation roots"
```

---

### Task 2: Document the supporting `docs/testing` directories

**Files:** Create or modify `README.md` in these exact directories:

```text
docs/testing/cleanup
docs/testing/failure-evidence
docs/testing/legacy
docs/testing/manual
docs/testing/plans
docs/testing/runbooks
docs/testing/source-material
docs/testing/strategies
docs/testing/test-reports
docs/testing/ux-evaluation
docs/testing/workbook05
docs/testing/workbooks
docs/testing/workbooks/generated
docs/testing/workbooks/text-templates
```

**Interfaces:**

- Consumes: `docs/testing/README.md`, registers, plans, workbooks and existing folder guides.
- Produces: complete navigation from the testing root to non-publication documentation files.

- [ ] **Step 1: Inventory immediate children and files for all 14 directories**

Run a read-only PowerShell inventory that prints each directory followed by its immediate directories and files. Save no generated inventory in the repository.

- [ ] **Step 2: Read source material needed for accurate descriptions**

Inspect file headings, CSV headers, workbook manifests and existing README content. For DOCX/XLSX/PDF files, use filenames plus adjacent manifests or source documents; do not claim internal content that was not verified.

- [ ] **Step 3: Write or expand the 14 README files**

Every README must explain all immediate items. The `legacy` guide must explain that historical evidence is preserved but is not automatically authoritative. The `workbooks` guides must distinguish templates, generated workbooks, completion registers and final-results workbooks. The `cleanup` guide must explain receipts without presenting cleanup scripts as test evidence.

- [ ] **Step 4: Check directory coverage**

Run:

```powershell
$dirs = @(
  'docs/testing/cleanup','docs/testing/failure-evidence','docs/testing/legacy',
  'docs/testing/manual','docs/testing/plans','docs/testing/runbooks',
  'docs/testing/source-material','docs/testing/strategies','docs/testing/test-reports',
  'docs/testing/ux-evaluation','docs/testing/workbook05','docs/testing/workbooks',
  'docs/testing/workbooks/generated','docs/testing/workbooks/text-templates'
)
$missing = $dirs | Where-Object { -not (Test-Path (Join-Path $_ 'README.md')) }
if ($missing) { throw "Missing README: $($missing -join ', ')" }
git diff --check
```

Expected: no missing README and no whitespace errors.

- [ ] **Step 5: Commit the supporting documentation**

```powershell
git add docs/testing
git commit -m "docs(testing): explain supporting documentation folders"
```

---

### Task 3: Complete README coverage for final-results routes 01–03

**Files:** Create or modify `README.md` in every directory listed below:

```text
docs/testing/final-results/01-upstream-llama-cpp
docs/testing/final-results/01-upstream-llama-cpp/data
docs/testing/final-results/01-upstream-llama-cpp/evidence
docs/testing/final-results/01-upstream-llama-cpp/evidence/failures
docs/testing/final-results/01-upstream-llama-cpp/evidence/failures/curated-logs
docs/testing/final-results/01-upstream-llama-cpp/evidence/source
docs/testing/final-results/01-upstream-llama-cpp/reports
docs/testing/final-results/01-upstream-llama-cpp/reproduction
docs/testing/final-results/01-upstream-llama-cpp/reproduction/protocol
docs/testing/final-results/01-upstream-llama-cpp/reproduction/quality
docs/testing/final-results/01-upstream-llama-cpp/reproduction/scripts
docs/testing/final-results/01-upstream-llama-cpp/reproduction/system
docs/testing/final-results/01-upstream-llama-cpp/validation
docs/testing/final-results/02-atomicbot-turboquant
docs/testing/final-results/02-atomicbot-turboquant/data
docs/testing/final-results/02-atomicbot-turboquant/evidence
docs/testing/final-results/02-atomicbot-turboquant/evidence/failures
docs/testing/final-results/02-atomicbot-turboquant/evidence/failures/curated-logs
docs/testing/final-results/02-atomicbot-turboquant/evidence/source
docs/testing/final-results/02-atomicbot-turboquant/reports
docs/testing/final-results/02-atomicbot-turboquant/reproduction
docs/testing/final-results/02-atomicbot-turboquant/reproduction/protocol
docs/testing/final-results/02-atomicbot-turboquant/reproduction/quality
docs/testing/final-results/02-atomicbot-turboquant/reproduction/scripts
docs/testing/final-results/02-atomicbot-turboquant/reproduction/system
docs/testing/final-results/02-atomicbot-turboquant/validation
docs/testing/final-results/03-animehacker-tq3-0
docs/testing/final-results/03-animehacker-tq3-0/data
docs/testing/final-results/03-animehacker-tq3-0/evidence
docs/testing/final-results/03-animehacker-tq3-0/evidence/failures
docs/testing/final-results/03-animehacker-tq3-0/evidence/failures/curated-logs
docs/testing/final-results/03-animehacker-tq3-0/evidence/source
docs/testing/final-results/03-animehacker-tq3-0/reports
docs/testing/final-results/03-animehacker-tq3-0/reproduction
docs/testing/final-results/03-animehacker-tq3-0/reproduction/protocol
docs/testing/final-results/03-animehacker-tq3-0/reproduction/quality
docs/testing/final-results/03-animehacker-tq3-0/reproduction/scripts
docs/testing/final-results/03-animehacker-tq3-0/reproduction/system
docs/testing/final-results/03-animehacker-tq3-0/validation
```

**Interfaces:**

- Consumes: route reports, route JSON, CSV schemas, evidence indexes, reproduction assets and validation receipts.
- Produces: continuous beginner navigation for the three `llama.cpp`-family publication packages.

- [ ] **Step 1: Reconfirm route accounting from `catalog/campaign-summary.csv`**

Expected: upstream 13 passed; AtomicBot 19 passed; animehacker 7 passed, 1 failed and 3 blocked.

- [ ] **Step 2: Inspect every immediate file and existing README**

Use CSV headers, JSON keys, Markdown headings and script help to explain what each file means. Explain DOCX/PDF/XLSX files through their source or provenance files.

- [ ] **Step 3: Write the route-level and leaf-level guides**

Use the same folder vocabulary across routes while preserving route-specific facts. Explain that failures remain evidence, quality methods are route-specific and a passed process does not prove product suitability.

- [ ] **Step 4: Verify all listed directories and route totals**

Run a PowerShell missing-README check over the exact path list above, then run `git diff --check` and compare all status claims to the canonical campaign summary.

- [ ] **Step 5: Commit routes 01–03**

```powershell
git add docs/testing/final-results/01-upstream-llama-cpp docs/testing/final-results/02-atomicbot-turboquant docs/testing/final-results/03-animehacker-tq3-0
git commit -m "docs(testing): explain llama route result packages"
```

---

### Task 4: Complete README coverage for final-results routes 04–06 and shared files

**Files:** Create or modify `README.md` in every directory listed below:

```text
docs/testing/final-results
docs/testing/final-results/04-openvino-experimental-fork
docs/testing/final-results/04-openvino-experimental-fork/data
docs/testing/final-results/04-openvino-experimental-fork/evidence
docs/testing/final-results/04-openvino-experimental-fork/evidence/source
docs/testing/final-results/04-openvino-experimental-fork/reports
docs/testing/final-results/04-openvino-experimental-fork/reproduction
docs/testing/final-results/04-openvino-experimental-fork/reproduction/protocol
docs/testing/final-results/04-openvino-experimental-fork/reproduction/quality
docs/testing/final-results/04-openvino-experimental-fork/reproduction/system
docs/testing/final-results/04-openvino-experimental-fork/validation
docs/testing/final-results/05-openvino-official-upstream
docs/testing/final-results/05-openvino-official-upstream/data
docs/testing/final-results/05-openvino-official-upstream/evidence
docs/testing/final-results/05-openvino-official-upstream/evidence/source
docs/testing/final-results/05-openvino-official-upstream/reports
docs/testing/final-results/05-openvino-official-upstream/reproduction
docs/testing/final-results/05-openvino-official-upstream/reproduction/protocol
docs/testing/final-results/05-openvino-official-upstream/reproduction/quality
docs/testing/final-results/05-openvino-official-upstream/reproduction/system
docs/testing/final-results/05-openvino-official-upstream/validation
docs/testing/final-results/06-cross-route-comparison
docs/testing/final-results/06-cross-route-comparison/data
docs/testing/final-results/06-cross-route-comparison/evidence
docs/testing/final-results/06-cross-route-comparison/reports
docs/testing/final-results/06-cross-route-comparison/reproduction
docs/testing/final-results/06-cross-route-comparison/reproduction/protocol
docs/testing/final-results/06-cross-route-comparison/validation
docs/testing/final-results/catalog
docs/testing/final-results/standards
docs/testing/final-results/standards/schemas
docs/testing/final-results/validation
```

**Interfaces:**

- Consumes: OpenVINO packages, comparison matrix, catalogues, schemas and release validation.
- Produces: complete publication-tree navigation and the authoritative beginner route to final results.

- [ ] **Step 1: Reconfirm release totals and comparison boundaries**

Expected totals: 169 rows; 81 passed, 6 failed, 28 blocked and 54 artifact-unavailable. Only the 15 matched experimental/official OpenVINO configurations are directly comparable for performance.

- [ ] **Step 2: Inspect every file and current guide**

Pay special attention to portable versus evidence-only workbooks, quality assets, system records, RO-Crate metadata, checksum manifests and validation receipts.

- [ ] **Step 3: Write all missing and expanded guides**

Explain the difference between experimental and official OpenVINO, why blocked/unavailable rows are not zero, why the comparison package is guarded, and where a beginner finds the human report versus machine-readable rows.

- [ ] **Step 4: Validate the complete final-results tree**

Run:

```powershell
python -m scripts.testing.cli.validate_results --route all --output-root docs/testing/final-results
git diff --check
```

Expected: existing final-results validation passes and Markdown has no whitespace errors.

- [ ] **Step 5: Commit routes 04–06 and shared guides**

```powershell
git add docs/testing/final-results
git commit -m "docs(testing): complete final results navigation"
```

---

### Task 5: Document every supported testing-script directory

**Files:** Create or modify `README.md` in these exact directories:

```text
scripts/testing
scripts/testing/campaigns
scripts/testing/campaigns/animehacker
scripts/testing/campaigns/atomicbot
scripts/testing/campaigns/llama_cpp
scripts/testing/campaigns/openvino
scripts/testing/cli
scripts/testing/examples
scripts/testing/reporting
scripts/testing/tests
scripts/testing/tests/acceptance
scripts/testing/tests/fixtures
scripts/testing/tests/integration
scripts/testing/tests/unit
scripts/testing/tools
scripts/testing/turbovec
scripts/testing/workbook05
scripts/testing/workbook05/phase3
```

**Interfaces:**

- Consumes: script source, module docstrings, CLI `--help`, test names and existing safety guidance.
- Produces: a beginner-readable map from supported commands to lower-level implementation and tests.

- [ ] **Step 1: Inventory and classify every immediate script**

Classify each file as supported CLI, campaign implementation, report builder, validation utility, compatibility wrapper, test, fixture or Workbook 05 helper.

- [ ] **Step 2: Read source before describing behaviour**

For each script, inspect its module docstring, argument parser or exported functions. Do not infer mutation behaviour from its name. For each large folder, group related tools but still list every filename.

- [ ] **Step 3: Write all 18 guides**

Each script description must answer: what it does, normal caller, important input, output, whether it writes files or launches a runtime, and the preferred high-level command if direct use is discouraged.

- [ ] **Step 4: Verify names and command help**

Check that every immediate non-README file appears in its directory README. Run `--help` only for supported CLIs that do not perform work when help is requested. Run the existing focused testing CLI tests.

```powershell
python -m pytest scripts/testing/tests/acceptance/test_testing_cli.py -q
git diff --check
```

- [ ] **Step 5: Commit script guides**

```powershell
git add scripts/testing
git commit -m "docs(testing): explain testing scripts for beginners"
```

---

### Task 6: Expand the main experiment-library guides

**Files:** Create or modify `README.md` in these exact directories:

```text
experiments
experiments/figures
experiments/figures/animehacker-tq3-0
experiments/figures/atomicbot-turboquant
experiments/figures/cross-route-comparison
experiments/figures/custom-openvino-turboquant
experiments/figures/official-openvino
experiments/figures/upstream-llama-cpp
experiments/manifests
experiments/manifests/animehacker-tq3-0
experiments/manifests/atomicbot-turboquant
experiments/manifests/cross-route-comparison
experiments/manifests/custom-openvino-turboquant
experiments/manifests/official-openvino
experiments/manifests/turbovec
experiments/manifests/upstream-llama-cpp
experiments/patches
experiments/patches/openvino-cpu-state-observer
experiments/patches/openvino-turboquant
experiments/processed-results
experiments/processed-results/animehacker-tq3-0
experiments/processed-results/atomicbot-turboquant
experiments/processed-results/context
experiments/processed-results/cross-route
experiments/processed-results/cross-route-comparison
experiments/processed-results/custom-openvino-turboquant
experiments/processed-results/EST-VALID-001
experiments/processed-results/EXP-TQ-COMP-001
experiments/processed-results/EXP-TV-COMP-001
experiments/processed-results/final-metrics
experiments/processed-results/memory-budgets
experiments/processed-results/official-openvino
experiments/processed-results/upstream-llama-cpp
experiments/prompts
experiments/protocols
experiments/protocols/animehacker-tq3-0
experiments/protocols/atomicbot-turboquant
experiments/protocols/cross-route-comparison
experiments/protocols/custom-openvino-turboquant
experiments/protocols/official-openvino
experiments/protocols/turbovec
experiments/protocols/upstream-llama-cpp
experiments/rubrics
experiments/scripts
experiments/scripts/animehacker-tq3-0
experiments/scripts/atomicbot-turboquant
experiments/scripts/cross-route-comparison
experiments/scripts/custom-openvino-turboquant
experiments/scripts/official-openvino
experiments/scripts/upstream-llama-cpp
```

**Interfaces:**

- Consumes: experiment protocols, manifests, processed outputs, figure indexes, rubrics and legacy wrappers.
- Produces: navigation across the reusable experiment-definition and processed-output layers.

- [ ] **Step 1: Inspect all immediate files and existing guides**

Read JSON schemas/keys, CSV headers, Markdown headings and script interfaces. Identify planned but empty experiment packages and state that they contain no completed result instead of describing them as successful.

- [ ] **Step 2: Expand existing guides without losing authority statements**

Retain source-of-truth links, route statuses and supersession notes. Add complete file tables, format explanations and beginner navigation.

- [ ] **Step 3: Add the missing semantic guides**

Create guides for `manifests/turbovec`, `patches`, and `protocols/turbovec`. Explain that patch assets are controlled inputs, not proof that a patched build was used.

- [ ] **Step 4: Verify coverage and links**

Run an exact-path README presence check for the list above, a relative-link resolver, and `git diff --check`.

- [ ] **Step 5: Commit main experiment guides**

```powershell
git add experiments
git commit -m "docs(testing): expand experiment library guides"
```

---

### Task 7: Document operational and retained experiment navigation without touching run folders

**Files:** Create or modify `README.md` in these exact directories:

```text
experiments/granite_turboquant_intel
experiments/granite_turboquant_intel/configurations
experiments/granite_turboquant_intel/configurations/workbook05
experiments/granite_turboquant_intel/logs
experiments/granite_turboquant_intel/logs/upstream-llama-cpp
experiments/granite_turboquant_intel/manifests
experiments/granite_turboquant_intel/manifests/builds
experiments/granite_turboquant_intel/manifests/campaigns
experiments/granite_turboquant_intel/manifests/environments
experiments/granite_turboquant_intel/manifests/runs
experiments/granite_turboquant_intel/manifests/templates
experiments/granite_turboquant_intel/notes
experiments/granite_turboquant_intel/notes/upstream-llama-cpp
experiments/granite_turboquant_intel/processed-results
experiments/granite_turboquant_intel/processed-results/upstream-llama-cpp
experiments/granite_turboquant_intel/prompts
experiments/granite_turboquant_intel/prompts/fixtures
experiments/granite_turboquant_intel/prompts/rendered
experiments/granite_turboquant_intel/prompts/rendered-v2
experiments/granite_turboquant_intel/rubrics
experiments/granite_turboquant_intel/schemas
experiments/granite_turboquant_intel/schemas/workbook05
experiments/raw-results
experiments/raw-results/animehacker-tq3-0
experiments/raw-results/atomicbot-turboquant
experiments/raw-results/cross-route-comparison
experiments/raw-results/custom-openvino-turboquant
experiments/raw-results/EXP-OV-OFFICIAL-001
experiments/raw-results/failure-records
experiments/raw-results/official-openvino
experiments/raw-results/openvino-community
experiments/raw-results/openvino-conversion
experiments/raw-results/openvino-official
experiments/raw-results/openvino-turboquant
experiments/raw-results/quality
experiments/raw-results/retained
experiments/raw-results/retained/animehacker-tq3-0
experiments/raw-results/retained/atomicbot-turboquant
experiments/raw-results/retained/openvino-experimental-fork
experiments/raw-results/retained/openvino-official-upstream
experiments/raw-results/retained/shared
experiments/raw-results/retained/upstream-llama-cpp
experiments/raw-results/turbovec
```

**Interfaces:**

- Consumes: operational campaign evidence and retained raw-evidence layout.
- Produces: safe navigation to generated evidence without changing individual evidence captures.

- [ ] **Step 1: Build the exclusion audit**

List all descendant directories matching run IDs, attempt IDs, dates, timestamps, samples, warm-ups, pilots and caches. Record the list outside the repository for the duration of verification only.

- [ ] **Step 2: Inspect semantic folders and generated naming patterns**

For each listed directory, identify immediate files and immediate generated child patterns. Explain the pattern, not every repeated run folder. State which newer curated package supersedes operational evidence where applicable.

- [ ] **Step 3: Write the listed guides**

Use strong raw-evidence warnings. Tell beginners to start with `docs/testing/final-results` for conclusions and use these folders only for audit, diagnosis or reproduction.

- [ ] **Step 4: Prove excluded directories were not modified**

Run:

```powershell
$changed = git diff --name-only HEAD
$bad = $changed | Where-Object {
  $_ -match '(__pycache__|/sample-[^/]+/|/warmup/|/pilot/|/\d{4}-\d{2}-\d{2}/)' -or
  ($_ -notmatch 'README\.md$')
}
if ($bad) { throw "Out-of-scope changes: $($bad -join ', ')" }
git diff --check
```

Expected: only approved README paths are reported.

- [ ] **Step 5: Commit operational navigation**

```powershell
git add experiments/granite_turboquant_intel experiments/raw-results
git commit -m "docs(testing): explain operational evidence folders"
```

---

### Task 8: Audit the complete README hierarchy and repair every finding

**Files:**

- Modify only the README files created or expanded in Tasks 1–7.
- Do not modify testing data or executable files.

**Interfaces:**

- Consumes: every README from Tasks 1–7 and all canonical source evidence.
- Produces: the final verified beginner documentation hierarchy.

- [ ] **Step 1: Run the coverage audit**

Create an in-memory exact scope list from this plan. Verify that each path contains `README.md`. For every README, compare its `## Folders` and `## Files` inventory to the actual immediate contents. Allow only documented generated-pattern grouping.

- [ ] **Step 2: Run the link audit**

Parse relative Markdown links from every changed README. Resolve each link from the README's directory and fail on any missing target. Ignore external URLs only after confirming they use HTTPS and contain no credentials or tokens.

- [ ] **Step 3: Run the factual audit**

Recheck:

- 169 intended attempts;
- 81 passed, 6 failed, 28 blocked and 54 artifact-unavailable;
- route-level totals;
- 15 directly comparable OpenVINO performance configurations;
- release ID `unified-final-results-2026-09-01-v2`;
- TurboVec decision `DEMONSTRATOR_ONLY`; and
- incomplete estimator-validation, APP-EVAL and standalone `EXP-TQ-COMP-001` boundaries.

- [ ] **Step 4: Run the beginner-language review and repair loop**

Read every changed README as if the reader knows Git and CSV only by name. List and fix unexplained acronyms, vague descriptions, missing authority labels, unsafe commands, duplicated passages and unclear navigation. Repeat until no material finding remains.

- [ ] **Step 5: Run repository verification**

```powershell
python -m pytest scripts/testing/tests/unit scripts/testing/tests/integration scripts/testing/tests/acceptance -q
python -m scripts.testing.cli.validate_results --route all --output-root docs/testing/final-results
git diff --check
git status --short
```

Expected: tests and final-results validation pass; every changed implementation file is a `README.md`; the only other documentation changes are the approved design and plan commits.

- [ ] **Step 6: Commit final review repairs**

```powershell
git add docs/testing scripts/testing experiments
git commit -m "docs(testing): finalize beginner README hierarchy"
```

- [ ] **Step 7: Report the final inventory**

Report:

- number of README files created and modified;
- all excluded directory classes;
- verification commands and results;
- any remaining limitation; and
- final commit range.
