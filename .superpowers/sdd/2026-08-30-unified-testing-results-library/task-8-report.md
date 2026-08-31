# Task 8 Report: OpenVINO master reports

## Status

Implemented and validated both evidence-bound OpenVINO master reports.

## Implementation

- Added `build_openvino_report(bundle)` in `scripts/testing/final_results/openvino_report.py`.
- The builder consumes normalized `RouteBundle` records and verified repository/hardware/software metadata. Campaign measurements, counts, extrema, case scores, status totals, aggregations, evidence hashes, and table values are derived from those records.
- Frozen sector and prompt-length labels are keyed by the normalized `OPENVINO-SECTOR-EXPERIENCE-QUALITY-v3` identity; coverage counts are computed only over prompt IDs present in the bundle.
- Implemented the approved 15-section order, complete attempt/performance/repetition/quality/failure/evidence tables, explicit campaign authority, comparison boundaries, and revision history.
- Preserved the fv6 distinction between `Artifact unavailable` and execution failure. Preserved the official distinction between conversion `Failed` and hardware-preflight `Blocked`; no official blocked case is called unavailable.
- Added an atomic, exact route-manifest regeneration helper for the deliberate Task 8 artifact-set transition.
- Extended the reviewed DOCX status styling so the controlled display label `Artifact unavailable` receives the intended neutral-grey fill without changing its explicit text. Existing Passed/Failed/Blocked styling is unchanged.
- Updated the official route's exact-inventory regression to include the five planned Task 8 artifacts.

## TDD evidence

Initial report-content RED:

```text
6 failed in 0.20s
```

All failures were the expected missing-module contract: `openvino_report` did not exist.

First report GREEN:

```text
6 passed in 6.23s
```

Visual QA then exposed orphan section headings at portrait-to-landscape transitions. A regression requiring contextual lead paragraphs before wide-table sections produced:

```text
1 failed in 1.11s
```

After the report-content correction:

```text
7 passed in 7.38s
```

Status-colour QA exposed the missing neutral fill for the exact controlled label. The new renderer regression produced:

```text
1 failed in 0.20s
```

After the minimal shared-renderer correction:

```text
1 passed in 0.17s
```

## Verification

Focused report and DOCX suite:

```text
14 passed in 7.74s
```

Relevant models/CSV/evidence/Markdown/experimental/official/report non-Word regression:

```text
175 passed in 91.22s
```

`compileall` completed successfully. `git diff --check` reported no whitespace errors; Git emitted only configured line-ending conversion warnings for existing LF-controlled generated files.

## Render, parity, and Word safety

| Route | Markdown/DOCX parity | PDF pages | Portrait | Landscape | Blank pages |
| --- | --- | ---: | ---: | ---: | ---: |
| Experimental fv6 | Passed | 54 | 12 | 42 | 0 |
| Official fv2/fv1 | Passed | 52 | 12 | 40 | 0 |

- Both PDFs contain searchable titles and the expected attempt, performance, quality, failure, evidence, and revision headings.
- Both DOCX files were exported through the reviewed owned-process Word exporter with a 180-second timeout.
- The pre-existing Word baseline was PID 5032 before and after every final export. No additional WINWORD process remained.

## Visual inspection

- Rendered every page of both PDFs to temporary PNGs with PyMuPDF.
- Inspected all numbered pages, including both title pages and all 82 landscape table pages.
- Corrected the initial orphan-heading finding, rerendered, and verified no remaining orphan heading.
- Verified no overflow beyond margins, no clipped text, no blank page, and repeated table headers on continuation pages.
- Verified explicit status text plus colour: experimental Passed green / Artifact unavailable neutral grey; official Passed green / Failed red / Blocked amber.
- Dense attempt, repetition, failure, and evidence tables remain searchable and within landscape margins.
- Temporary PNGs and contact sheets were created outside the repository, excluded from every route manifest and commit, and removed after final inspection using exact validated paths beneath the OS temporary directory.

## Manifest audit

Each route contains 28 files total: 27 files covered by its checksum receipt plus the checksum manifest itself.

```text
04-openvino-experimental-fork: 27 entries; exact file-set match true; hash errors []
05-openvino-official-upstream: 27 entries; exact file-set match true; hash errors []
```

The five Task 8 additions per route are:

- `workbook/source/<route>-final-report.md`
- `workbook/generated/<route>-final-report.docx`
- `workbook/generated/<route>-final-report.pdf`
- `validation/workbook-parity.json`
- `validation/validation-report.md`

## Generated artifact inventory

### Experimental OpenVINO fork

- `docs/testing/final-results/04-openvino-experimental-fork/workbook/source/openvino-experimental-fork-final-report.md`
- `docs/testing/final-results/04-openvino-experimental-fork/workbook/generated/openvino-experimental-fork-final-report.docx`
- `docs/testing/final-results/04-openvino-experimental-fork/workbook/generated/openvino-experimental-fork-final-report.pdf`
- `docs/testing/final-results/04-openvino-experimental-fork/validation/workbook-parity.json`
- `docs/testing/final-results/04-openvino-experimental-fork/validation/validation-report.md`
- Updated `docs/testing/final-results/04-openvino-experimental-fork/evidence/manifest-sha256.txt`

### Official upstream OpenVINO

- `docs/testing/final-results/05-openvino-official-upstream/workbook/source/openvino-official-upstream-final-report.md`
- `docs/testing/final-results/05-openvino-official-upstream/workbook/generated/openvino-official-upstream-final-report.docx`
- `docs/testing/final-results/05-openvino-official-upstream/workbook/generated/openvino-official-upstream-final-report.pdf`
- `docs/testing/final-results/05-openvino-official-upstream/validation/workbook-parity.json`
- `docs/testing/final-results/05-openvino-official-upstream/validation/validation-report.md`
- Updated `docs/testing/final-results/05-openvino-official-upstream/evidence/manifest-sha256.txt`

## Self-review and concerns

- No measured campaign number is embedded as an authoritative prose constant; mutation coverage proves performance and quality tables respond to normalized-record changes.
- Quality scoring preserves the 5/3/2 weighting, criterion applicability, per-check contribution increments, 48-prompt arithmetic aggregation, sectors, lengths, rubric/version identity, and objective output-health boundary.
- Complete evidence indexes make the audit reports long (54 and 52 pages), but exact values and hashes remain readable, searchable, and available without a second document.
- All temporary inspection PNG directories were removed after validation; no inspection image is present in the repository, staged set, or route manifests.
- Existing unrelated recovered worktree changes were not staged or modified.

## Fix round 1

- Corrected reproduction and route-artifact references to use the verified numbered route directories: `04-openvino-experimental-fork` and `05-openvino-official-upstream`. A generated-report regression extracts every repository-local path and verifies that it exists.
- Restricted DOCX status fills to data rows. Header cells remain blue with white text even when their labels are `Passed`, `Failed`, `Blocked`, or `Artifact unavailable`; status data cells retain their controlled fills with explicit dark text and at least 4.5:1 contrast.
- Path-integrity TDD produced the expected prefix RED (`1 failed`) and GREEN (`1 passed`). Header/data-style TDD produced the expected header-fill RED (`E2F0D9 != 0B63CE`) and GREEN (`3 passed` with the existing controlled-label checks).
- Regenerated both canonical Markdown, DOCX, PDF, and parity artifacts. The owned Word exporter preserved baseline PID 5032.
- Re-rendered and inspected all 106 PDF pages in contact sheets. Availability pages 6 and 7 in each report and representative experimental/official status-data pages were reviewed at full resolution; no clipping, overflow, blank page, orphan heading, or status-colour regression was found.
- Focused report/DOCX verification passed 16 tests; the relevant final-results regression passed 184 tests in 88.35 seconds.
