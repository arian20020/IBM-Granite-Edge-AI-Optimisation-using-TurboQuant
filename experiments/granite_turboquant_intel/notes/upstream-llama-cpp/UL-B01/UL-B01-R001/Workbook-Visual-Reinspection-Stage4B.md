# Workbook Visual Reinspection - Stage 4B

| Field | Value |
|---|---|
| Run ID | UL-B01-R001 |
| Failure ID | FAIL-CTRL-001 |
| Branch | testing/workbook-control-baseline-repair |
| Base commit | 4bee757fb8ad40986df33df7e352526bfa4e7338 |
| Inspection date | 2026-07-14 |
| Workbooks inspected | 3 |
| Total pages inspected | 19 |
| Overall result | PASS |
| Current classification | Blocked pending controlled commit, push, PR and merge |

## Inspection results

| Workbook | Revision | Pages | Result | SHA-256 | Notes |
|---|---:|---:|---|---|---|
| WB-01 | 1.2 | 6 | PASS | 94b30e517187e0c88bf0d95d2f7c4983b9baf5dc9e2a366157ad9669f3e0cf19 | Correct metadata; all pages readable; no clipping or overlap. |
| WB-02 | 1.2 | 8 | PASS | ff060829358f5a3c722371a0367ac0acf9709c0108d343d7309dc66838c9b36e | Correct complete source hash; quality tables and all pages readable. |
| WB-03 | 1.2 | 5 | PASS | 3f1786b5c0e66759919387c3379441152b9af450b3ec3596d3a458c78a73b17c | Correct metadata and visible TQ3_0 title; all pages readable. |

## Stage 4B corrections confirmed

- Controlled filename is shown in WB-01, WB-02 and WB-03.
- Generated DOCX hash ownership is assigned to Controlled-Workbook-Manifest.csv.
- Original source filename and complete source SHA-256 are shown.
- Legacy generated-output hash metadata is absent.
- WB-03 displays TQ3_0 correctly.

## General visual checks

- All 19 pages rendered successfully.
- No clipped rows, columns or tables were found.
- No overlapping text was found.
- Revision histories are complete and readable.
- Headers and footers remain correctly positioned.
- No unexplained blank or malformed page was found.

## Non-blocking observations

- Long references wrap inside revision-history cells.
- Literal Markdown backticks remain visible around some metadata values.
- WB-02 page 8 contains unused space because the final section is short.
- Some narrow table headings wrap heavily but remain readable.

## Gate decision

The Stage 4B visual-reinspection gate passes.

FAIL-CTRL-001 remains open until the controlled commit, push, pull request and merge lifecycle are complete.

No llama.cpp build, IBM Granite inference, hardware benchmark, performance measurement or model-quality evaluation was performed.
