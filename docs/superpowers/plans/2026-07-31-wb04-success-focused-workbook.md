# WB-04 Success-Focused Workbook Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the dense WB-04 v1.7 presentation with a controlled v1.8 workbook that shows verified successes in compact aggregate tables, summarizes incomplete tests as short ID-and-reason bullets, and retains full governed evidence validation.

**Architecture:** Keep the existing release-evidence builder and reconciler responsible for the complete 60-ID, 36-runtime, and 33-quality evidence set. After reconciliation, select exactly three admissible measured runtime rows for presentation, render eight concise sections, and validate the presentation independently from the full evidence gate. Generate the same controlled DOCX filename, update revision WR-036 and all three CSV registers, then run structural DOCX and full test-suite verification.

**Tech Stack:** Python 3.13, `unittest`/`pytest`, Markdown, `python-docx`, deterministic OOXML ZIP normalization, JSON/SHA-256 evidence records, CSV repository registers, PowerShell verification commands.

## Global Constraints

- Controlled visible identity is workbook version `1.8`, revision `WR-036`, superseding `1.7` / `WR-035`.
- The controlled filename remains `04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`.
- Full evidence validation remains exactly 60 controlled IDs, 36 runtime outcomes, 33 quality outcomes, three measured runtime rows, seven expected rejections, and 26 terminal/not-launched runtime rows.
- Numeric presentation rows are limited to OV-TQ-13/512, OV-TQ-14/512, and OV-TQ-14/2048.
- Each numeric row must remain `accepted=true`, `status=measured`, three accepted samples, `fallback=false`, complete aggregate metrics, cleanup verified, and hash-bound to its measurement summary.
- No governed P1-P6 campaign completed; no numeric quality score, quality-qualified pass, winner, or statement that all tests passed may appear.
- Preserve raw attempts, per-sample metrics, terminal evidence, and exact values under `experiments/raw-results/openvino-turboquant/2026-07-30/` and `2026-07-31/`.
- Do not rerun model workloads and do not modify WB-01, WB-02, WB-03, WB-05, or WB-06 content.
- Edit repository CSV registers as text and keep every WB-04 register field populated.
- Preserve unrelated working-tree changes and stage only task-owned paths for each commit.

---

### Task 1: Add the v1.8 presentation selector and compact measured-results renderer

**Files:**
- Modify: `scripts/testing/finalize_official_openvino_workbook.py`
- Modify: `scripts/testing/tests/test_finalize_official_openvino_workbook.py`

**Interfaces:**
- Consumes: reconciled `Mapping[RuntimeKey, RuntimeOutcome]` produced by `reconcile_runtime_rows`.
- Produces: `select_presentation_measurements(rows) -> tuple[RuntimeOutcome, ...]`, `render_section_5(rows) -> str`, and `validate_section_5(text) -> dict[str, int]`.

- [ ] **Step 1: Write failing selector and renderer tests**

Add tests that build the three existing measured fixtures and assert the exact presentation keys, three tables, three rows per table, complete aggregate metrics, short `E1`-`E3` references, and absence of per-sample rows or terminal literals.

```python
def test_v18_selector_admits_only_the_three_hash_bound_measurements(self):
    from scripts.testing.finalize_official_openvino_workbook import (
        RuntimeKey,
        select_presentation_measurements,
    )

    rows = self._three_measured_runtime_outcomes()
    selected = select_presentation_measurements(rows)
    self.assertEqual(
        [row.key for row in selected],
        [
            RuntimeKey("OV-TQ-13", 512),
            RuntimeKey("OV-TQ-14", 512),
            RuntimeKey("OV-TQ-14", 2048),
        ],
    )
    self.assertTrue(all(row.sample_count == 3 for row in selected))
    self.assertTrue(all(row.activation["fallback"] is False for row in selected))


def test_v18_section_5_contains_three_compact_aggregate_tables(self):
    section = render_section_5(self._three_measured_runtime_outcomes())
    report = validate_section_5(section)
    self.assertEqual(report, {"table_count": 3, "measured_row_count": 3})
    self.assertEqual(section.count("| OV-TQ-13 | 512 |"), 3)
    self.assertEqual(section.count("| OV-TQ-14 | 512 |"), 3)
    self.assertEqual(section.count("| OV-TQ-14 | 2048 |"), 3)
    self.assertNotIn("Sample ID", section)
    self.assertNotIn("not-produced-by-", section)
```

- [ ] **Step 2: Run the new tests and confirm the expected failure**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_finalize_official_openvino_workbook.py -k "v18_selector or v18_section_5"
```

Expected: failure because the v1.8 selector and Section 5 functions do not exist.

- [ ] **Step 3: Implement strict presentation selection**

Add the exact key constant and selector beside the existing runtime rendering helpers.

```python
PRESENTATION_MEASURED_KEYS = (
    RuntimeKey("OV-TQ-13", 512),
    RuntimeKey("OV-TQ-14", 512),
    RuntimeKey("OV-TQ-14", 2048),
)


def select_presentation_measurements(
    rows: Mapping[RuntimeKey, RuntimeOutcome],
) -> tuple[RuntimeOutcome, ...]:
    measured_keys = {key for key, row in rows.items() if row.status == "measured"}
    if measured_keys != set(PRESENTATION_MEASURED_KEYS):
        raise ValueError("v1.8 presentation measured-key set is invalid")
    selected = tuple(rows[key] for key in PRESENTATION_MEASURED_KEYS)
    for row in selected:
        if not row.accepted or row.sample_count != 3 or row.cleanup_process_count != 0:
            raise ValueError(f"{row.key} is not an accepted three-sample measurement")
        if row.activation.get("fallback") is not False:
            raise ValueError(f"{row.key} does not prove fallback=false")
        required = set(REQUIRED_SCALAR_METRICS) | set(UTILISATION_METRICS)
        if not required.issubset(row.metrics):
            raise ValueError(f"{row.key} aggregate metrics are incomplete")
    return selected
```

Use a display helper that rounds finite numeric values to at most three decimals while leaving the stored `RuntimeOutcome` unchanged.

- [ ] **Step 4: Render and validate the three aggregate tables**

Implement Section 5 with these exact table headers:

```python
TIMING_HEADERS = (
    "Test ID", "Context", "Load ms", "TTFT ms", "Prompt tok/s",
    "TPOT ms", "Decode tok/s", "Generation ms",
)
MEMORY_HEADERS = (
    "Test ID", "Context", "Peak WS MiB", "Peak private MiB",
    "Available RAM min MiB", "KV MiB", "Cleanup",
)
UTILISATION_HEADERS = (
    "Test ID", "Context", "CPU mean/median/peak %",
    "GPU mean/median/peak %", "CPU samples", "GPU samples",
    "Accepted runs", "Fallback count", "Evidence ref",
)
```

`validate_section_5` must require exactly three tables, identical measured key sets in all three, exactly three rows per table, no blank/bare `N/A` cells, and evidence labels exactly `E1`, `E2`, `E3`.

- [ ] **Step 5: Run focused tests**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_finalize_official_openvino_workbook.py -k "v18_selector or v18_section_5"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit the selector and metrics renderer**

```powershell
git add -- scripts/testing/finalize_official_openvino_workbook.py scripts/testing/tests/test_finalize_official_openvino_workbook.py
git commit -m "feat(openvino): render compact WB-04 measured results"
```

---

### Task 2: Render compact incomplete-test, quality-boundary, and decision sections

**Files:**
- Modify: `scripts/testing/finalize_official_openvino_workbook.py`
- Modify: `scripts/testing/tests/test_finalize_official_openvino_workbook.py`

**Interfaces:**
- Consumes: fully reconciled runtime and quality mappings, canonical matrix IDs, and release-input path/hash.
- Produces: `render_section_6`, `render_section_7`, `render_section_8`, and `validate_presentation_text`.

- [ ] **Step 1: Write failing tests for the compact limitation list**

Assert that Section 6 contains six real Markdown bullets, every non-success test ID, one primary reason per bullet, no table, and no per-attempt hash.

```python
def test_v18_incomplete_tests_are_six_compact_reason_bullets(self):
    section = render_section_6(runtime_rows, expected_ids)
    bullets = [line for line in section.splitlines() if line.startswith("- ")]
    self.assertEqual(len(bullets), 6)
    self.assertIn("OV-TQ-03, OV-TQ-04", section)
    self.assertIn("RAM safety floor", section)
    self.assertIn("larger host", section)
    self.assertNotIn("| Test ID |", section)
    self.assertNotRegex(section, r"[0-9a-f]{64}")
```

Expand every controlled ID literally in the rendered text; do not rely on range prose such as “03 through 12,” because release coverage validation must be able to find every ID.

- [ ] **Step 2: Write failing tests for the quality boundary and evidence index**

```python
def test_v18_quality_boundary_refuses_numeric_or_winner_claims(self):
    section = render_section_7(quality_rows)
    self.assertIn("No governed P1-P6 quality campaign completed", section)
    self.assertIn("no numeric quality score", section)
    self.assertIn("no winner", section)
    self.assertNotRegex(section, r"\b[0-9]+(?:\.[0-9]+)?\s*/\s*10\b")


def test_v18_decision_has_exact_e1_e3_hash_bound_sources(self):
    section = render_section_8(runtime_rows, release_input_path)
    self.assertIn("| E1 |", section)
    self.assertIn("| E2 |", section)
    self.assertIn("| E3 |", section)
    self.assertEqual(len(re.findall(r"[0-9a-f]{64}", section)), 4)
    self.assertNotIn("quality-qualified winner", section.casefold())
```

- [ ] **Step 3: Run the new tests and confirm they fail**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_finalize_official_openvino_workbook.py -k "v18_incomplete or v18_quality_boundary or v18_decision"
```

Expected: failure because the new section renderers are absent.

- [ ] **Step 4: Implement Section 6 from exact governed groups**

Use immutable ID tuples for these six reasons:

```python
PRESENTATION_INCOMPLETE_GROUPS = (
    ("Diagnostic only; no formal benchmark", ("OV-01",)),
    ("Missing validated FP16 artifact", ("OV-C01", "OV-02")),
    ("RAM safety floor reached", (
        "OV-B04", "OV-03", "OV-06", "OV-TQ-03", "OV-TQ-04", "OV-TQ-05",
        "OV-TQ-06", "OV-TQ-07", "OV-TQ-08", "OV-TQ-09", "OV-TQ-10",
        "OV-TQ-11", "OV-TQ-12", "OV-TQ-13", "OV-TQ-14", "OV-TQ-15",
    )),
    ("Larger host required", (
        "OV-C04", "OV-C05", "OV-C06", "OV-07", "OV-08", "OV-09",
        "OV-10", "OV-TQ-16", "OV-TQ-17",
    )),
    ("Strict activation proof incomplete", (
        "OV-B08", "OV-B09", "OV-B10", "OV-B12", "OV-TQS-01",
        "OV-TQS-02", "OV-TQS-03", "OV-TQS-04",
    )),
    ("Governed quality campaign stopped at the RAM floor", (
        "OV-TQ-13/512", "OV-TQ-14/512", "OV-TQ-14/2048",
    )),
)
```

The renderer must preserve contexts where one test ID has mixed outcomes, especially OV-TQ-13 and OV-TQ-14.

- [ ] **Step 5: Implement Sections 7 and 8 with prohibited-claim validation**

`render_section_7` must reject any numeric `QualityOutcome.mean_score`. `render_section_8` must include a two-column final decision table followed by a two-column evidence index whose E1-E3 rows use `_evidence_cell` values and whose reconciliation row hashes the exact release-input file.

Add `validate_presentation_text` checks for:

```python
PROHIBITED_PRESENTATION_CLAIMS = (
    "all tests passed",
    "quality-qualified pass",
    "quality winner",
)
```

The validator must also confirm that all 60 canonical IDs are visible across success tables, negative controls, and the compact incomplete-test bullets.

- [ ] **Step 6: Run the focused tests**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_finalize_official_openvino_workbook.py -k "v18_incomplete or v18_quality_boundary or v18_decision or prohibited"
```

Expected: all selected tests pass.

- [ ] **Step 7: Commit the narrative renderers**

```powershell
git add -- scripts/testing/finalize_official_openvino_workbook.py scripts/testing/tests/test_finalize_official_openvino_workbook.py
git commit -m "feat(openvino): summarize WB-04 limitations and quality boundary"
```

---

### Task 3: Change the release contract and canonical Markdown to the eight-section v1.8 view

**Files:**
- Modify: `scripts/testing/build_official_openvino_release_evidence.py`
- Modify: `scripts/testing/finalize_official_openvino_workbook.py`
- Modify: `scripts/testing/examples/official-openvino-wb04-reconciliation-input.example.json`
- Modify: `.superpowers/sdd/2026-07-19-openvino-turboquant-recovery/wb04-static-sections-draft.md`
- Modify: `docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md`
- Modify: `scripts/testing/tests/test_build_official_openvino_release_evidence.py`
- Modify: `scripts/testing/tests/test_finalize_official_openvino_workbook.py`

**Interfaces:**
- Consumes: the unchanged full matrix/runtime/quality evidence inventory.
- Produces: release-input schema `official-openvino-wb04-release-input/v2`, four hash-bound static sections, and a finalized eight-section Markdown workbook.

- [ ] **Step 1: Write failing identity and static-contract tests**

Update tests to require:

```python
self.assertEqual(release["schema"], "official-openvino-wb04-release-input/v2")
self.assertEqual(release["workbook_version"], "1.8")
self.assertEqual(release["revision_id"], "WR-036")
self.assertEqual(set(release["static_section_bodies"]), {"1", "2", "3", "4"})
```

Add a template test that requires headings `# 1.` through `# 8.` exactly once and rejects headings `# 9.` through `# 15.`.

- [ ] **Step 2: Run the contract tests and confirm v1.7 fails**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_build_official_openvino_release_evidence.py scripts/testing/tests/test_finalize_official_openvino_workbook.py -k "identity or static or eight_section or release_input_example"
```

Expected: failures showing v1.7, WR-035, the v1 schema, and the old static-section set.

- [ ] **Step 3: Update the release evidence identity and static-section contract**

Apply these constants in both builder and finalizer:

```python
WORKBOOK_VERSION = TARGET_WORKBOOK_VERSION = "1.8"
REVISION_ID = TARGET_REVISION_ID = "WR-036"
RELEASE_INPUT_SCHEMA = "official-openvino-wb04-release-input/v2"
STATIC_SECTION_EVIDENCE_SCHEMA = "official-openvino-wb04-static-section-evidence/v2"
STATIC_SECTION_NUMBERS = (1, 2, 3, 4)
```

Update error messages so they name v1.8/WR-036 and sections 1-4. Keep all runtime, expected-rejection, terminal, and quality counts unchanged.

- [ ] **Step 4: Rewrite the static draft as four concise source-bound sections**

The four static sections are:

1. Repository, runtime, and host: one compact facts table.
2. Successful build and recovery checks: one table containing only verified positive gates and source-suite/observer results.
3. Successful bounded diagnostics: one OV-C02/OV-C03 table with diagnostic-only caveats.
4. Successful expected-rejection controls: one three-row grouped table with every expected-rejection ID written explicitly.

Every table cell must be nonblank and must not contain a bare `N/A`.

- [ ] **Step 5: Rewrite the canonical Markdown shell**

Use exactly these headings and page breaks:

```markdown
# 04 Official OpenVINO Controlled Retest Workbook v1.8

Controlled retest revision 1.8 (WR-036).

# 1. Repository, runtime and host
# 2. Successful build and recovery checks
# 3. Successful bounded diagnostics
# 4. Successful expected-rejection controls
[[PAGEBREAK]]
# 5. Accepted formal runtime measurements
# 6. Tests that did not complete
# 7. Quality boundary
[[PAGEBREAK]]
# 8. Final decision and evidence index
```

The finalizer replaces Sections 1-4 from hash-bound static evidence and Sections 5-8 from reconciled outcomes. Update `_heading_bounds` so Section 8 ends at end-of-file.

- [ ] **Step 6: Integrate the dynamic renderers into `finalize_release`**

Keep reconciliation in the existing order, then render:

```python
for number in (1, 2, 3, 4):
    text = _replace_section_body(
        text,
        number,
        validate_static_section_body(number, bodies[str(number)]),
    )
text = _replace_section_body(text, 5, render_section_5(runtime))
text = _replace_section_body(text, 6, render_section_6(runtime, expected_ids))
text = _replace_section_body(text, 7, render_section_7(quality))
text = _replace_section_body(
    text,
    8,
    render_section_8(runtime, release_path),
)
report = validate_presentation_text(text, expected_ids)
```

Return both full evidence counts and presentation counts in the finalizer report.

- [ ] **Step 7: Update the example release input and tests**

Keep the same top-level keys, use schema v2, version 1.8, revision WR-036, and four static body records. Update fixture evidence schemas to v2 wherever the fixture is testing current release behavior.

- [ ] **Step 8: Run release-builder and finalizer tests**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_build_official_openvino_release_evidence.py scripts/testing/tests/test_finalize_official_openvino_workbook.py
```

Expected: all tests pass and the reports retain 60/36/33 full evidence counts with three presentation measurements.

- [ ] **Step 9: Commit the v1.8 release contract**

```powershell
git add -- scripts/testing/build_official_openvino_release_evidence.py scripts/testing/finalize_official_openvino_workbook.py scripts/testing/examples/official-openvino-wb04-reconciliation-input.example.json .superpowers/sdd/2026-07-19-openvino-turboquant-recovery/wb04-static-sections-draft.md docs/testing/workbooks/text-templates/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.md scripts/testing/tests/test_build_official_openvino_release_evidence.py scripts/testing/tests/test_finalize_official_openvino_workbook.py
git commit -m "feat(openvino): define WB-04 v1.8 presentation contract"
```

---

### Task 4: Update the DOCX release profile and structural presentation audit

**Files:**
- Modify: `scripts/testing/audit_official_openvino_docx.py`
- Modify: `scripts/testing/official_openvino/docx_audit.py`
- Modify: `scripts/testing/tests/test_official_openvino_docx.py`
- Modify: `scripts/testing/tests/test_build_official_openvino_release_evidence.py`

**Interfaces:**
- Consumes: generated v1.8 DOCX, manifest row, canonical matrix, and visible workbook text.
- Produces: release audit requiring revision 1.8, 10 DOCX tables, zero blank cells, all 60 IDs represented, three measured result rows, compact limitations, and prohibited-claim absence.

- [ ] **Step 1: Write failing v1.8 DOCX-profile tests**

Change the release-profile expectations to:

```python
self.assertEqual(profile.expected_visible_revision, "1.8")
self.assertEqual(profile.expected_table_count, 10)
```

Add a fixture whose visible text contains the three measured keys and compact limitation bullets. Assert rejection when the DOCX contains `all tests passed`, a numeric quality score, or omits the “no winner” disclosure.

- [ ] **Step 2: Run the DOCX tests and confirm the old profile fails**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_docx.py
```

Expected: failure because the wrapper still requires v1.7 and 22 tables.

- [ ] **Step 3: Implement the v1.8 release audit**

Set:

```python
RELEASE_VISIBLE_REVISION = "1.8"
RELEASE_TABLE_COUNT = 10
```

Extend the generic audit report with presentation checks without weakening ZIP integrity, blank-cell, revision-history, manifest-hash, or full controlled-ID validation. Require visible text for all three measured `(test ID, context)` pairs and the exact no-score/no-winner note.

- [ ] **Step 4: Run DOCX and builder integration tests**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_official_openvino_docx.py scripts/testing/tests/test_build_official_openvino_release_evidence.py
```

Expected: all tests pass.

- [ ] **Step 5: Commit the release profile**

```powershell
git add -- scripts/testing/audit_official_openvino_docx.py scripts/testing/official_openvino/docx_audit.py scripts/testing/tests/test_official_openvino_docx.py scripts/testing/tests/test_build_official_openvino_release_evidence.py
git commit -m "test(openvino): audit WB-04 v1.8 presentation"
```

---

### Task 5: Regenerate governed evidence and synchronize the controlled registers

**Files:**
- Modify: `experiments/raw-results/openvino-turboquant/2026-07-30/reconciliation-input.json`
- Modify: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-01.json`
- Modify: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-02.json`
- Modify: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-03.json`
- Modify: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-04.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-05.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-06.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-07.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-08.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-09.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-10.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-13.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-14.json`
- Delete: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections/section-15.json`
- Modify: `docs/testing/Workbook-Revision-Register.csv`
- Modify: `docs/testing/Workbook-Completion-Register.csv`
- Modify: `.superpowers/sdd/2026-07-19-openvino-turboquant-recovery/progress.md`

**Interfaces:**
- Consumes: the tested v1.8 builder/finalizer and existing hash-bound raw evidence.
- Produces: deterministic v2 reconciliation input, four static-section evidence records, WR-036 revision state, and eight completion-register rows. The manifest remains untouched until the final Markdown and DOCX hashes exist in Task 6.

- [ ] **Step 1: Generate fresh v2 release evidence**

Run:

```powershell
python scripts/testing/build_official_openvino_release_evidence.py
python scripts/testing/finalize_official_openvino_workbook.py --check-only
```

Expected: builder reports the unchanged 36 runtime and 33 quality rows; finalizer accepts 60 IDs and reports three presentation measurements.

- [ ] **Step 2: Remove only obsolete static-section JSON records**

Delete the nine explicitly listed obsolete static-section files with `apply_patch`. Do not remove any runtime, quality, terminal, expected-rejection, or provenance evidence.

- [ ] **Step 3: Update the revision register as repository text**

Change WR-035 status to `Superseded` and append WR-036 with:

- version 1.8;
- date 2026-07-31;
- change type `Success-focused controlled presentation`;
- a summary stating three measured runtime rows, grouped successful boundaries, compact incomplete-test reasons, full evidence retained, and no quality score/winner;
- affected IDs covering all WB-04 families;
- change reference containing the exact v2 reconciliation-input SHA-256;
- status `Current - pending merge`;
- supersedes `1.7`.

- [ ] **Step 4: Replace the 15 WB-04 completion rows with eight complete rows**

Use section IDs 1-8 and the exact v1.8 section titles. Every CSV field must be nonblank. `Missing_Data_Code` is `None`; the notes must distinguish the three runtime measurements from the absent quality score.

- [ ] **Step 5: Update the progress ledger**

Record the presentation redesign, unchanged evidence counts, new section/table counts, no workload reruns, and pending final hashes.

- [ ] **Step 6: Validate the revision and completion CSV slices**

Run a PowerShell check that imports the revision and completion CSVs, selects `Workbook_ID -eq 'WB-04'`, rejects blank values, requires one current revision, eight unique completion sections, and no literal `N/A`.

- [ ] **Step 7: Commit evidence and register updates**

Stage only the v2 reconciliation/static evidence, three registers, and progress ledger.

```powershell
git add -- experiments/raw-results/openvino-turboquant/2026-07-30/reconciliation-input.json experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/static-sections docs/testing/Workbook-Revision-Register.csv docs/testing/Workbook-Completion-Register.csv .superpowers/sdd/2026-07-19-openvino-turboquant-recovery/progress.md
git commit -m "docs(openvino): register WB-04 v1.8 presentation"
```

---

### Task 6: Generate, structurally verify, and deliver the polished DOCX

**Files:**
- Modify: `docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx`
- Modify: `docs/testing/workbooks/Controlled-Workbook-Manifest.csv`
- Modify: `experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/docx-structural-qa.json`
- Modify: `.superpowers/sdd/2026-07-19-openvino-turboquant-recovery/progress.md`

**Interfaces:**
- Consumes: finalized v1.8 Markdown, WR-036 revision history, and the v1.8 DOCX audit profile.
- Produces: deterministic 10-table controlled DOCX, exact manifest hashes, structural-QA record, and final verification evidence.

- [ ] **Step 1: Finalize Markdown and generate the DOCX**

Run:

```powershell
python scripts/testing/finalize_official_openvino_workbook.py
python scripts/testing/Generate-Controlled-Workbooks.py --workbook-id WB-04
python scripts/testing/Apply-Workbook-Revision-History.py --workbook-id WB-04
```

Expected: the canonical Markdown contains eight sections and nine content tables; the DOCX contains ten tables including revision history.

- [ ] **Step 2: Compute and write exact manifest hashes**

Compute SHA-256 for the canonical Markdown and generated DOCX. Patch only the WB-04 manifest row with those exact lowercase hashes, then confirm both match with `Get-FileHash`.

- [ ] **Step 3: Run finalizer and structural DOCX release gates**

Run:

```powershell
python scripts/testing/finalize_official_openvino_workbook.py --check-only
python scripts/testing/audit_official_openvino_docx.py --release
```

Expected finalizer facts: 60 controlled IDs, 36 runtime rows, three measured rows, seven expected rejections, 26 terminal rows, 33 quality rows, three Section 5 metric tables, and zero blank cells.

Expected DOCX facts: visible revision 1.8, ten tables, zero blank cells, valid revision history, matching manifest hash, and ZIP integrity passed.

- [ ] **Step 4: Attempt packaged DOCX rendering**

Run:

```powershell
python C:\Users\Student\.codex\plugins\cache\openai-primary-runtime\documents\26.709.11516\skills\documents\render_docx.py docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx --output_dir .tmp/wb04-v18-render
```

If LibreOffice/`soffice` is still unavailable, retain the structural audit, do not claim page-level visual verification, and record the limitation in the progress ledger and final response.

- [ ] **Step 5: Run focused and full automated tests**

Run:

```powershell
python -m pytest -q scripts/testing/tests/test_build_official_openvino_release_evidence.py scripts/testing/tests/test_finalize_official_openvino_workbook.py scripts/testing/tests/test_official_openvino_docx.py
python -m pytest -q scripts/testing/tests
```

Expected: every current test passes; the known `TestCase` collection warning may remain if no test fails.

- [ ] **Step 6: Run final repository-integrity checks**

Run:

```powershell
git diff --check
git status --short
Get-Process | Where-Object { $_.ProcessName -match 'python|benchmark_app|openvino|ovms|llama' }
```

Expected: no diff errors and no residual model/runtime processes. Review status to ensure no unrelated file was accidentally staged or removed.

- [ ] **Step 7: Commit the final controlled artifact and verified hashes**

```powershell
git add -- docs/testing/workbooks/generated/04_Official_OpenVINO_Controlled_Retest_Workbook_v1.docx docs/testing/workbooks/Controlled-Workbook-Manifest.csv experiments/raw-results/openvino-turboquant/2026-07-30/release-evidence/docx-structural-qa.json .superpowers/sdd/2026-07-19-openvino-turboquant-recovery/progress.md
git commit -m "docs(openvino): publish polished WB-04 v1.8 workbook"
```

- [ ] **Step 8: Report the controlled result accurately**

Report that the workbook now foregrounds three runtime measurements, preserves all requested aggregate metrics, summarizes incomplete tests compactly, and retains full raw evidence. State explicitly that no quality score/winner exists and whether visual page rendering was unavailable.
