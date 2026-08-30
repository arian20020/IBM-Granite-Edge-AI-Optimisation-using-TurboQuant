# Unified Testing Results Library Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a professional, evidence-bound results library containing synchronized Markdown, DOCX, PDF, CSV, JSON, and Excel deliverables for three llama.cpp routes and two OpenVINO routes, plus a guarded cross-route comparison.

**Architecture:** A new `scripts/testing/final_results` package converts existing immutable campaign evidence into normalized attempt, measurement, result, quality, failure, system, and evidence records. Canonical Markdown reports are rendered deterministically to DOCX, exported to PDF through installed Microsoft Word, and validated for content parity, evidence integrity, coverage, and cross-route comparability. Generated artifacts live under `docs/testing/final-results`; existing controlled workbooks and raw evidence remain untouched.

**Tech Stack:** Python 3.11+, standard library, `pytest`, `jsonschema`, `python-docx`, `openpyxl`, PowerShell 5.1, Microsoft Word COM PDF export, OOXML inspection, SHA-256, Markdown, CSV, JSON, JSON-LD/RO-Crate 1.3.

**Spec:** `docs/superpowers/specs/2026-08-30-unified-testing-results-library-design.md`

## Global Constraints

- Preserve all existing controlled workbooks, raw results, registers, and uncommitted work.
- Do not rerun benchmarks or modify benchmark implementations.
- Create a new unified final-results series; do not replace WB-01 through WB-05.
- Experimental OpenVINO canonical source is `experiments/raw-results/openvino-experimental-fork/2026-08-30/fv6` with 81 cases: 27 `passed` and 54 `model_artifact_unavailable`.
- Official OpenVINO canonical status source is `experiments/raw-results/openvino-official-upstream/2026-08-30/fv2-missing-model-attempts/consolidated` with 45 cases: 15 `passed`, 5 `conversion_failed`, and 25 `hardware_preflight_blocked`; detailed quality and raw run evidence remain sourced from `fv1`.
- Treat Markdown as canonical report content; generated DOCX and PDF must not be edited independently.
- Retain both existing OpenVINO Excel workbooks as source deliverables.
- Never represent missing, blocked, unavailable, unexecuted, not-applicable, or historically uncollected results as zero.
- Keep route-specific quality methodologies and block unsupported cross-route score rankings.
- Use repository-relative evidence paths and SHA-256 hashes; do not publish absolute machine paths.
- Do not claim formal ACM, FAIR, MLCommons, NIST, RO-Crate, W3C, IETF, or independent-reproduction certification.
- Only stage and commit files belonging to the current task; the worktree contains unrelated recovered changes.
- Use `$Python = (Resolve-Path '.tools/python311-portable/python.exe').Path` in commands after Task 1 installs the pinned reporting requirements into that private, untracked tool environment.

## File and responsibility map

- `scripts/testing/final_results/models.py`: typed canonical records and status vocabulary.
- `scripts/testing/final_results/csvio.py`: deterministic CSV/JSON serialization and schema validation.
- `scripts/testing/final_results/openvino_adapter.py`: normalize experimental fv6 and official fv2/fv1 evidence.
- `scripts/testing/final_results/llama_adapter.py`: normalize controlled registers and route-specific llama.cpp evidence.
- `scripts/testing/final_results/evidence.py`: portable paths, hashes, provenance, and claim-evidence maps.
- `scripts/testing/final_results/report_model.py`: report sections, tables, notes, and rendering-neutral content.
- `scripts/testing/final_results/markdown_renderer.py`: canonical Markdown writer.
- `scripts/testing/final_results/docx_renderer.py`: professional Word renderer and deterministic package normalization.
- `scripts/testing/final_results/validate.py`: coverage, derivation, integrity, parity, and comparability checks.
- `scripts/testing/build_final_results.py`: route-selectable orchestration CLI.
- `scripts/testing/Export-Final-Results-Pdf.ps1`: bounded hidden Word PDF export.
- `scripts/testing/tests/test_final_results_*.py`: focused unit and integration tests.
- `docs/testing/final-results/standards/`: schemas, vocabularies, metric definitions, and policies.
- `docs/testing/final-results/<route>/`: route reports, canonical data, evidence, failures, reproduction notes, and validation receipts.
- `docs/testing/final-results/catalog/`: collection-wide summaries and claim/evidence registry.
- `docs/testing/final-results/validation/`: global validation and release-readiness receipts.

---

## Phase A: Shared contracts and deterministic tooling

### Task 1: Reporting dependencies and canonical record contracts

**Files:**
- Modify: `scripts/testing/requirements.txt`
- Create: `scripts/testing/final_results/__init__.py`
- Create: `scripts/testing/final_results/models.py`
- Create: `scripts/testing/tests/test_final_results_models.py`

**Interfaces:**
- Produces: `Status`, `AttemptRecord`, `MeasurementRecord`, `SummaryRecord`, `QualityRecord`, `FailureRecord`, `EvidenceRecord`, and `RouteBundle`.
- Produces: `Status.from_source(value: str) -> Status` and each record's `to_row() -> dict[str, object]`.

- [ ] **Step 1: Pin the report toolchain**

Append these exact requirements:

```text
jsonschema==4.25.1
openpyxl==3.1.5
PyMuPDF==1.26.4
pypdf==6.0.0
pytest==8.4.2
```

Keep the existing `lxml`, `python-docx`, and `typing_extensions` pins unchanged.

- [ ] **Step 2: Write failing status and record tests**

Create tests asserting:

```python
def test_source_statuses_map_without_losing_failure_kind():
    assert Status.from_source("passed") is Status.PASSED
    assert Status.from_source("model_artifact_unavailable") is Status.ARTIFACT_UNAVAILABLE
    assert Status.from_source("conversion_failed") is Status.FAILED
    assert Status.from_source("hardware_preflight_blocked") is Status.BLOCKED

def test_non_passed_attempt_requires_reason():
    with pytest.raises(ValueError, match="reason"):
        AttemptRecord(
            route_id="openvino-official-upstream",
            campaign_id="2026-08-30-fv2",
            test_case_id="granite-3b__fp16__tbq3",
            attempt_id="granite-3b__fp16__tbq3-attempt-1",
            status=Status.FAILED,
            executed=False,
            reason="",
        )
```

- [ ] **Step 3: Run the tests and confirm the contract is absent**

Run:

```powershell
$Python = (Resolve-Path '.tools/python311-portable/python.exe').Path
& $Python -m pip install --disable-pip-version-check -r scripts/testing/requirements.txt
& $Python -m pytest scripts/testing/tests/test_final_results_models.py -q
```

Expected: collection fails because `scripts.testing.final_results.models` does not exist.

- [ ] **Step 4: Implement immutable typed records**

Implement `Status` as a string enum with display labels and source aliases. Implement frozen dataclasses with explicit IDs and optional metric fields. Enforce:

```python
if self.status is not Status.PASSED and not self.reason.strip():
    raise ValueError("non-passed attempt requires a reason")
if self.status is Status.PASSED and not self.executed:
    raise ValueError("passed attempt must be executed")
```

`RouteBundle` must contain tuples for attempts, measurements, summaries, quality, failures, and evidence plus dictionaries for repository, hardware, and software metadata.

- [ ] **Step 5: Run and commit**

Run the focused test, then:

```powershell
git add -- scripts/testing/requirements.txt scripts/testing/final_results/__init__.py scripts/testing/final_results/models.py scripts/testing/tests/test_final_results_models.py
git commit -m "feat: define final results data contracts"
```

### Task 2: Schemas, deterministic serialization, and standard vocabulary

**Files:**
- Create: `scripts/testing/final_results/csvio.py`
- Create: `scripts/testing/tests/test_final_results_csvio.py`
- Create: `docs/testing/final-results/standards/README.md`
- Create: `docs/testing/final-results/standards/data-dictionary.md`
- Create: `docs/testing/final-results/standards/status-taxonomy.md`
- Create: `docs/testing/final-results/standards/metric-definitions.md`
- Create: `docs/testing/final-results/standards/provenance-policy.md`
- Create: `docs/testing/final-results/standards/quality-comparison-policy.md`
- Create: `docs/testing/final-results/standards/schemas/*.schema.json`

**Interfaces:**
- Produces: `write_csv(path: Path, rows: Iterable[Mapping[str, object]], fieldnames: Sequence[str]) -> None`.
- Produces: `write_json(path: Path, payload: object) -> None`.
- Produces: `validate_json(instance: object, schema_path: Path) -> list[str]`.

- [ ] **Step 1: Write failing deterministic-output tests**

Test that UTF-8 CSV uses `\n`, preserves declared column order, writes booleans as lowercase `true`/`false`, writes `None` as an empty field, and produces byte-identical output twice. Test that schema errors return JSON-pointer-like paths.

- [ ] **Step 2: Run the focused test**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_csvio.py -q
```

Expected: FAIL because serialization functions do not exist.

- [ ] **Step 3: Implement serializers and seven Draft 2020-12 schemas**

Schemas must require stable IDs, use `additionalProperties: false`, encode the controlled normalized statuses, and distinguish nullable measurements from status fields. `attempts.schema.json` must require `reason` conditionally whenever `status != "passed"`.

- [ ] **Step 4: Write the standards documents with exact semantics**

Define medians over included repetitions, worst-observed peak memory as `max(peak_working_set_bytes)`, and blanks as unavailable observations rather than zero. Define the display-status mapping and the rule that quality scores are rankable only when prompt suite, rubric, scoring version, denominator, and aggregation all match.

- [ ] **Step 5: Run tests and commit**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_csvio.py -q
git add -- scripts/testing/final_results/csvio.py scripts/testing/tests/test_final_results_csvio.py docs/testing/final-results/standards
git commit -m "feat: add final results schemas and standards"
```

### Task 3: Evidence inventory, portable provenance, and integrity manifests

**Files:**
- Create: `scripts/testing/final_results/evidence.py`
- Create: `scripts/testing/tests/test_final_results_evidence.py`

**Interfaces:**
- Produces: `repo_relative(repo_root: Path, path: Path) -> str`.
- Produces: `hash_file(path: Path) -> str`.
- Produces: `build_evidence_record(...) -> EvidenceRecord`.
- Produces: `write_sha256_manifest(root: Path, paths: Sequence[Path], output: Path) -> None`.
- Produces: `validate_sha256_manifest(root: Path, manifest: Path) -> list[str]`.

- [ ] **Step 1: Write failure-first provenance tests**

Test rejection of paths outside the repository, Windows absolute paths in serialized records, duplicate evidence IDs, missing files, and incorrect hashes. Test manifest lines are sorted and use `/` separators:

```python
assert manifest.read_text() == f"{digest}  evidence/a.json\n"
```

- [ ] **Step 2: Run the focused test and observe failure**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_evidence.py -q
```

- [ ] **Step 3: Implement streaming SHA-256 and provenance records**

Hash in 1 MiB blocks. Resolve both root and target before containment checks. Reject symlink or resolution escapes. Make evidence IDs stable from route ID plus the SHA-256 prefix, resolving a collision by using the full digest.

- [ ] **Step 4: Run and commit**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_evidence.py -q
git add -- scripts/testing/final_results/evidence.py scripts/testing/tests/test_final_results_evidence.py
git commit -m "feat: add portable evidence provenance"
```

### Task 4: Rendering-neutral report model and canonical Markdown

**Files:**
- Create: `scripts/testing/final_results/report_model.py`
- Create: `scripts/testing/final_results/markdown_renderer.py`
- Create: `scripts/testing/tests/test_final_results_markdown.py`

**Interfaces:**
- Produces: `Report`, `ReportSection`, `ReportParagraph`, `ReportTable`, and `ReportNote`.
- Produces: `render_markdown(report: Report, output: Path) -> None`.
- Consumes: normalized rows only; it must not parse raw evidence.

- [ ] **Step 1: Write the canonical Markdown fixture test**

Build a two-section report in the test and assert exact output including title, document-control table, a status table, escaped pipes, explicit `Not collected`, evidence IDs, and final newline. Assert the renderer rejects unequal table-row widths.

- [ ] **Step 2: Run the test to verify failure**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_markdown.py -q
```

- [ ] **Step 3: Implement the report model and deterministic Markdown renderer**

`Report` must carry route ID, revision, generated date, sections, and evidence IDs. Tables carry stable `table_id`, title, subtitle, columns, rows, and footnotes. Escape `|` as `\|` and line breaks as `<br>`.

- [ ] **Step 4: Run and commit**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_markdown.py -q
git add -- scripts/testing/final_results/report_model.py scripts/testing/final_results/markdown_renderer.py scripts/testing/tests/test_final_results_markdown.py
git commit -m "feat: render canonical final result reports"
```

### Task 5: Professional DOCX renderer, parity parser, and PDF exporter

**Files:**
- Create: `scripts/testing/final_results/docx_renderer.py`
- Create: `scripts/testing/final_results/parity.py`
- Create: `scripts/testing/Export-Final-Results-Pdf.ps1`
- Create: `scripts/testing/tests/test_final_results_docx.py`
- Create: `scripts/testing/tests/test_final_results_pdf_export.py`

**Interfaces:**
- Produces: `render_docx(report: Report, output: Path) -> None`.
- Produces: `compare_markdown_docx(markdown: Path, docx: Path) -> dict[str, object]`.
- PDF script accepts `-DocxPath`, `-PdfPath`, and `-TimeoutSeconds`.

- [ ] **Step 1: Write failing DOCX structure and parity tests**

Assert navy `0F2747`, teal `0F766E`, blue `0B63CE`, explicit status text, repeating table headers, non-splitting rows, page numbering, route/revision footer, deterministic ZIP timestamps, and exact Markdown/DOCX heading and cell parity.

- [ ] **Step 2: Run the DOCX tests and confirm failure**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_docx.py -q
```

- [ ] **Step 3: Implement the renderer**

Use portrait sections by default and insert landscape sections only for tables exceeding six columns or 95 estimated characters. Apply colour plus explicit status labels. Normalize DOCX ZIP members to timestamp `1980-01-01T00:00:00` after saving.

- [ ] **Step 4: Implement bounded Word PDF export**

The PowerShell script must start Word invisibly through COM, open read-only, call `ExportAsFixedFormat(..., 17)`, close the document, quit Word, and release COM objects in `finally`. Run export in a child PowerShell process from tests so `TimeoutSeconds` can terminate only the owned exporter process.

- [ ] **Step 5: Test PDF export and commit**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_docx.py scripts/testing/tests/test_final_results_pdf_export.py -q
git add -- scripts/testing/final_results/docx_renderer.py scripts/testing/final_results/parity.py scripts/testing/Export-Final-Results-Pdf.ps1 scripts/testing/tests/test_final_results_docx.py scripts/testing/tests/test_final_results_pdf_export.py
git commit -m "feat: generate and verify final report documents"
```

---

## Phase B: OpenVINO master reports

### Task 6: Normalize the experimental OpenVINO fv6 campaign

**Files:**
- Create: `scripts/testing/final_results/openvino_adapter.py`
- Create: `scripts/testing/tests/test_final_results_openvino_experimental.py`
- Generate: `docs/testing/final-results/04-openvino-experimental-fork/{protocol,system,results,quality,failures,evidence,reproduction,validation}/...`

**Interfaces:**
- Produces: `build_experimental_bundle(repo_root: Path) -> RouteBundle`.
- Reads only fv6 detailed, comparison, coverage, quality-detail, rows, raw-result, and input evidence.

- [ ] **Step 1: Write the fv6 acceptance test**

Assert 81 attempts, 27 passed/executed, 54 artifact-unavailable/unexecuted, nine cache formats, three model-weight artifacts with executed cases, 48 quality prompts per passed case, and source row/hash preservation. Assert unavailable rows have no performance or quality measurements.

- [ ] **Step 2: Run the test to verify failure**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_openvino_experimental.py -q
```

- [ ] **Step 3: Implement experimental normalization**

Map each detailed row to one attempt and summary record. Extract repetitions and raw result evidence from each referenced raw JSON. Expand quality details into `QualityRecord` rows keyed by case, prompt, category, and criterion. Record the 54 unavailable cases as attempts without fabricated measurements.

- [ ] **Step 4: Write route files and evidence manifests**

Generate `attempts.csv`, `measurements.csv`, `summary-results.csv`, `availability-matrix.csv`, `scores.csv`, `prompt-suite.csv`, `outputs-index.csv`, repository/system manifests, evidence index, claim map, and SHA-256 manifest. Copy the existing experimental Excel workbook into `results/source/` without altering its bytes.

- [ ] **Step 5: Run tests and commit**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_openvino_experimental.py -q
git add -- scripts/testing/final_results/openvino_adapter.py scripts/testing/tests/test_final_results_openvino_experimental.py docs/testing/final-results/04-openvino-experimental-fork
git commit -m "feat: normalize experimental OpenVINO results"
```

### Task 7: Normalize official OpenVINO fv2 status and fv1 measurement evidence

**Files:**
- Modify: `scripts/testing/final_results/openvino_adapter.py`
- Create: `scripts/testing/tests/test_final_results_openvino_official.py`
- Generate: `docs/testing/final-results/05-openvino-official-upstream/{protocol,system,results,quality,failures,evidence,reproduction,validation}/...`

**Interfaces:**
- Produces: `build_official_bundle(repo_root: Path) -> RouteBundle`.
- Uses fv2 consolidated rows for final statuses and fv1 for the 15 passed raw measurements and quality evidence.

- [ ] **Step 1: Write the official campaign acceptance test**

Assert 45 attempts, 15 passed, 5 conversion failures, and 25 hardware-preflight blocks. Assert only the 15 passed rows have measurements and quality scores. Assert TurboQuant formats are `tbq3` and `tbq4`; PolarQuant and QJL are absent. Assert all missing-model attempts retain failure stage, reason, and manifest evidence.

- [ ] **Step 2: Run the focused test and confirm failure**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_openvino_official.py -q
```

- [ ] **Step 3: Implement the two-source join**

Join on `case_id`. Final status, executed flag, failure stage, and failure reason come from fv2. Performance, quality, and raw-path fields come from fv1 only when fv2 status is passed. Raise an error if a passed fv2 case lacks fv1 raw evidence or if a non-passed row contains published metric values.

- [ ] **Step 4: Generate route files and retain both official workbooks**

Copy the revised `Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx` as the primary source workbook. Also index, but do not duplicate, the original fv1 workbook. Populate failure and preflight evidence from the missing-attempt manifests.

- [ ] **Step 5: Run tests and commit**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_openvino_official.py -q
git add -- scripts/testing/final_results/openvino_adapter.py scripts/testing/tests/test_final_results_openvino_official.py docs/testing/final-results/05-openvino-official-upstream
git commit -m "feat: normalize official OpenVINO results"
```

### Task 8: Build and validate the two OpenVINO master reports

**Files:**
- Create: `scripts/testing/final_results/openvino_report.py`
- Create: `scripts/testing/tests/test_final_results_openvino_reports.py`
- Generate: `docs/testing/final-results/04-openvino-experimental-fork/workbook/...`
- Generate: `docs/testing/final-results/05-openvino-official-upstream/workbook/...`

**Interfaces:**
- Produces: `build_openvino_report(bundle: RouteBundle) -> Report`.
- Consumes only normalized route data and verified metadata.

- [ ] **Step 1: Write report-content tests**

Assert the approved 15-section order, explicit campaign identity, test counts, format/model availability, aggregation definitions, 48-prompt quality methodology, sector coverage, attempt failures, comparison boundaries, evidence IDs, and revision history. Assert the official report never calls blocked rows unavailable and the experimental report never calls unavailable rows failed.

- [ ] **Step 2: Run tests and confirm failure**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_openvino_reports.py -q
```

- [ ] **Step 3: Implement answer-first OpenVINO report construction**

Use the experimental and official normalized data to produce technical summaries, exact result tables, availability tables, quality rubric detail, failure accounting, and limitations. Derive all headline values from canonical rows; do not hard-code measured numbers in prose.

- [ ] **Step 4: Render Markdown, DOCX, and PDF**

Generate both reports. Run parity comparison and save `workbook-parity.json`. Export each DOCX with a 180-second owned-process timeout. Use PyMuPDF to confirm nonzero pages, searchable title text, no blank pages, and expected table headings.

- [ ] **Step 5: Visually inspect rendered pages**

Render every PDF page to PNG in a temporary directory, inspect title pages plus all landscape table pages, and record page count, overflow findings, clipped text, orphan headings, and status-colour/text checks in each `validation-report.md`. Temporary PNGs are not committed.

- [ ] **Step 6: Run and commit**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_openvino_reports.py scripts/testing/tests/test_final_results_docx.py -q
git add -- scripts/testing/final_results/openvino_report.py scripts/testing/tests/test_final_results_openvino_reports.py docs/testing/final-results/04-openvino-experimental-fork docs/testing/final-results/05-openvino-official-upstream
git commit -m "feat: publish OpenVINO final result reports"
```

---

## Phase C: llama.cpp report adaptation

### Task 9: Normalize upstream llama.cpp from controlled registers and evidence

**Files:**
- Create: `scripts/testing/final_results/llama_adapter.py`
- Create: `scripts/testing/tests/test_final_results_upstream_llama.py`
- Generate: `docs/testing/final-results/01-upstream-llama-cpp/...`

**Interfaces:**
- Produces: `build_upstream_llama_bundle(repo_root: Path) -> RouteBundle`.
- Reads WB-01 revisions, test/performance/failure/evidence registers, the current WB-01 Markdown, and `experiments/granite_turboquant_intel/logs/upstream-llama-cpp`.

- [ ] **Step 1: Write route acceptance tests**

Assert UL-01 through UL-13 appear in the intended matrix, every completed workload is represented, UL-08 is identified only as the best observed CPU route, UL-10 only as the best observed Intel GPU route, and UL-05 only as the recorded fallback. Assert absent raw-results folder data is not fabricated and repository-relative log paths resolve.

- [ ] **Step 2: Run, implement, and rerun**

Normalize register rows by `Test_ID`, expand included repetitions from the performance register, attach evidence-index records, and translate historical missing fields to `Not collected`. Run:

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_upstream_llama.py -q
```

- [ ] **Step 3: Generate the route package and report**

Adapt WB-01 evidence into the approved section order and visual template. Preserve its original quality methodology and claim boundaries. Generate Markdown, DOCX, PDF, canonical CSVs, manifests, and validation receipts.

- [ ] **Step 4: Commit**

```powershell
git add -- scripts/testing/final_results/llama_adapter.py scripts/testing/tests/test_final_results_upstream_llama.py docs/testing/final-results/01-upstream-llama-cpp
git commit -m "feat: publish upstream llama.cpp final report"
```

### Task 10: Normalize and report AtomicBot TurboQuant

**Files:**
- Modify: `scripts/testing/final_results/llama_adapter.py`
- Create: `scripts/testing/tests/test_final_results_atomicbot.py`
- Generate: `docs/testing/final-results/02-atomicbot-turboquant/...`

**Interfaces:**
- Produces: `build_atomicbot_bundle(repo_root: Path) -> RouteBundle`.
- Reads WB-02, controlled registers, `AtomicBot_Master_Summary.json`, formal summaries, utilization evidence, quality summaries, and indexed failure evidence.

- [ ] **Step 1: Write acceptance tests**

Assert all 19 runtime configurations are represented, observed utilization fields remain attached to their runs, formal pass/block counts reconcile with the current WB-02 revision, and limited/provisional quality evidence is labelled as such rather than upgraded to the OpenVINO rubric.

- [ ] **Step 2: Implement the AtomicBot adapter and run tests**

Join route evidence on configuration/test ID. Reject summary rows whose evidence hash conflicts with `Evidence-Index.csv`. Run:

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_atomicbot.py -q
```

- [ ] **Step 3: Generate, render, validate, and commit**

Produce the common route structure and synchronized report formats, then:

```powershell
git add -- scripts/testing/final_results/llama_adapter.py scripts/testing/tests/test_final_results_atomicbot.py docs/testing/final-results/02-atomicbot-turboquant
git commit -m "feat: publish AtomicBot TurboQuant final report"
```

### Task 11: Normalize and report animehacker TQ3_0

**Files:**
- Modify: `scripts/testing/final_results/llama_adapter.py`
- Create: `scripts/testing/tests/test_final_results_animehacker.py`
- Generate: `docs/testing/final-results/03-animehacker-tq3-0/...`

**Interfaces:**
- Produces: `build_animehacker_bundle(repo_root: Path) -> RouteBundle`.
- Reads WB-03, `reconciliation.json`, CPU/SYCL/Vulkan reconciliation evidence, runtime summaries, quality adjudications, and route evidence indexes.

- [ ] **Step 1: Write acceptance tests**

Assert seven runnable rows are completed, three rows retain their safety classifications, zero unresolved failures is not confused with zero historical failure attempts, rejected evidence is excluded from formal summaries but retained in the attempt/failure ledger, and missing OS metrics remain `Not collected`.

- [ ] **Step 2: Implement route normalization and run tests**

Use reconciliation records as status authority and runtime summaries as measurement authority. Require explicit inclusion status before a runtime contributes to formal statistics. Run:

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_animehacker.py -q
```

- [ ] **Step 3: Generate, render, validate, and commit**

```powershell
git add -- scripts/testing/final_results/llama_adapter.py scripts/testing/tests/test_final_results_animehacker.py docs/testing/final-results/03-animehacker-tq3-0
git commit -m "feat: publish animehacker TQ3 final report"
```

---

## Phase D: Cross-route catalog, metadata, and release

### Task 12: Cross-route comparability gate and comparison report

**Files:**
- Create: `scripts/testing/final_results/comparison.py`
- Create: `scripts/testing/tests/test_final_results_comparison.py`
- Generate: `docs/testing/final-results/06-cross-route-comparison/...`
- Generate: `docs/testing/final-results/catalog/*.csv`

**Interfaces:**
- Produces: `classify_comparability(left: RouteBundle, right: RouteBundle, metric: str) -> Comparability`.
- Produces: `build_cross_route_report(bundles: Sequence[RouteBundle]) -> Report`.

- [ ] **Step 1: Write comparability tests**

Assert throughput comparisons require compatible model, prompt/input length, output length, backend class, repetition treatment, and metric definition. Assert quality ranking additionally requires identical prompt set, rubric, scoring version, denominator, and aggregation. Assert OpenVINO-v3 quality versus legacy llama quality is `descriptive_only`.

- [ ] **Step 2: Implement the gate and catalog builders**

Return `direct`, `normalized_with_caveat`, `descriptive_only`, or `not_comparable` plus machine-readable reasons. Catalogs must aggregate attempt statuses without dropping failed or blocked rows.

- [ ] **Step 3: Generate the comparison report**

The report must emphasize validated route-level conclusions and comparability boundaries. It must not create a universal “best repository” score or rank incompatible quality scores.

- [ ] **Step 4: Test and commit**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_comparison.py -q
git add -- scripts/testing/final_results/comparison.py scripts/testing/tests/test_final_results_comparison.py docs/testing/final-results/06-cross-route-comparison docs/testing/final-results/catalog
git commit -m "feat: add guarded cross-route comparison"
```

### Task 13: Orchestration CLI and full validation gates

**Files:**
- Create: `scripts/testing/final_results/validate.py`
- Create: `scripts/testing/build_final_results.py`
- Create: `scripts/testing/tests/test_build_final_results.py`
- Generate: `docs/testing/final-results/validation/*`

**Interfaces:**
- CLI: `build_final_results.py --route {all,openvino,experimental-openvino,official-openvino,upstream-llama,atomicbot,animehacker,cross-route} --output-root PATH --validate-only`.
- Produces: `validate_route(route_root: Path) -> ValidationReport` and `validate_collection(root: Path) -> ValidationReport`.

- [ ] **Step 1: Write end-to-end fixture tests**

Use a temporary miniature two-route collection. Assert validation catches duplicate IDs, missing intended attempts, incorrect medians, non-passed measurements, missing evidence, hash mismatch, Markdown/DOCX parity mismatch, and unsupported cross-route ranking.

- [ ] **Step 2: Implement orchestration and validation**

Order gates as schema, IDs, coverage, derivation, status/failure consistency, availability, paths/hashes, claim coverage, workbook parity, PDF structure, comparability, and release readiness. Write JSON plus human-readable Markdown receipts. Exit `0` only when required gates pass; exit `1` for validation failure and `2` for invalid CLI use.

- [ ] **Step 3: Run all unit and integration tests**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_*.py -q
& $Python scripts/testing/build_final_results.py --route all --output-root docs/testing/final-results
& $Python scripts/testing/build_final_results.py --route all --output-root docs/testing/final-results --validate-only
```

Expected: all tests pass and validation exits `0` or records only explicitly approved non-blocking historical limitations.

- [ ] **Step 4: Commit**

```powershell
git add -- scripts/testing/final_results/validate.py scripts/testing/build_final_results.py scripts/testing/tests/test_build_final_results.py docs/testing/final-results/validation
git commit -m "feat: validate unified testing results"
```

### Task 14: Collection metadata, checksums, portal, and Downloads handoff

**Files:**
- Create: `docs/testing/final-results/README.md`
- Create: `docs/testing/final-results/CHANGELOG.md`
- Create: `docs/testing/final-results/REPRODUCING.md`
- Create: `docs/testing/final-results/LICENSES.md`
- Create conditionally: `docs/testing/final-results/CITATION.cff`
- Create: `docs/testing/final-results/ro-crate-metadata.json`
- Create: `docs/testing/final-results/manifest-sha256.txt`
- Modify: `scripts/testing/final_results/validate.py`
- Create: `scripts/testing/tests/test_final_results_release.py`

**Interfaces:**
- Produces: a navigable release root with machine-readable metadata and validated hashes.

- [ ] **Step 1: Write release metadata tests**

Assert every route and report is linked from the portal, every RO-Crate data entity uses a relative URI, every generated artifact identifies its source activity, every checksum validates, and licensing gaps are explicit. Test that `CITATION.cff` is omitted when verified author metadata is unavailable.

- [ ] **Step 2: Generate metadata and portal**

Write RO-Crate 1.3 JSON-LD for the collection, reports, canonical tables, software, repositories, hardware, and generation activities. Build the SHA-256 manifest last and exclude the manifest itself. Explain exact reproduction commands and the no-rerun scope boundary.

- [ ] **Step 3: Perform final visual and data QA**

Open both OpenVINO Excel workbooks read-only with `openpyxl`, verify expected sheets and formula references, and inspect all five PDF reports plus the cross-route PDF page by page. Record any non-blocking limitation in `release-readiness.json`; do not mark release ready if a report is missing, clipped, internally inconsistent, or hash-invalid.

- [ ] **Step 4: Run the full verification suite**

```powershell
& $Python -m pytest scripts/testing/tests/test_final_results_*.py -q
& $Python scripts/testing/build_final_results.py --route all --output-root docs/testing/final-results --validate-only
git diff --check
git status --short
```

Confirm `git status` still shows the user's pre-existing recovered changes and that this work has not deleted, reset, cleaned, or overwritten them.

- [ ] **Step 5: Commit the release portal**

```powershell
git add -- docs/testing/final-results/README.md docs/testing/final-results/CHANGELOG.md docs/testing/final-results/REPRODUCING.md docs/testing/final-results/LICENSES.md docs/testing/final-results/ro-crate-metadata.json docs/testing/final-results/manifest-sha256.txt scripts/testing/final_results/validate.py scripts/testing/tests/test_final_results_release.py
if (Test-Path -LiteralPath 'docs/testing/final-results/CITATION.cff') { git add -- docs/testing/final-results/CITATION.cff }
git commit -m "docs: finalize unified testing results library"
```

- [ ] **Step 6: Copy user-facing deliverables to Downloads**

After validation succeeds, copy rather than move:

```powershell
$Downloads = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads'
$Deliverables = Get-ChildItem -LiteralPath 'docs/testing/final-results' -Recurse -File |
    Where-Object { $_.Extension -in '.docx','.pdf','.xlsx' }
foreach ($File in $Deliverables) {
    Copy-Item -LiteralPath $File.FullName -Destination (Join-Path $Downloads $File.Name)
}
```

Re-hash each copied file and compare it with the repository source. Report exact Downloads paths and hashes to the user.

## Final self-review checklist

- [ ] Every section and acceptance criterion in the design spec maps to a task above.
- [ ] Experimental OpenVINO uses fv6, not the missing recovered fv4 path.
- [ ] Official OpenVINO uses fv2 final statuses and fv1 passed-run evidence.
- [ ] All five routes preserve failures, blocks, unavailable rows, and historical missing metrics.
- [ ] Markdown, DOCX, PDF, Excel, CSV, JSON, and JSON-LD responsibilities are explicit.
- [ ] No task requires rerunning a benchmark or changing raw evidence.
- [ ] Cross-route quality rankings are blocked unless methodologies match.
- [ ] Every code-producing task begins with a failing test and ends with focused verification and a scoped commit.
- [ ] No plan step stages unrelated dirty-worktree files.
