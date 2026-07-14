# Workbook Visual Inspection — Stage 4

| Field | Value |
|---|---|
| Run ID | UL-B01-R001 |
| Failure ID | FAIL-CTRL-001 |
| Branch | testing/workbook-control-baseline-repair |
| Base commit | 4bee757fb8ad40986df33df7e352526bfa4e7338 |
| Inspection date | 2026-07-14 |
| Workbooks inspected | 6 |
| Overall visual result | PASS |
| Run classification | Blocked pending diff review and controlled commit |

## Inspection results

| Workbook | Version | Pages reviewed | Result | SHA-256 | Notes |
|---|---:|---:|---|---|---|
| WB-01 | 1.2 | 6 | PASS | 52bb188687ca5dc73962a4ad33a95de8ea81ff66a4fe62aed97f6e29bc8372c9 | All pages clear; tables fit; no clipping, overlap or malformed pages. |
| WB-02 | 1.2 | 8 | PASS | 2ca8e1b831cc5b34ca0d1c6833533cd39422bbbb9f6c7213281f76c1670158a2 | Formal results and quality-rubric tables remain readable and inside margins. |
| WB-03 | 1.2 | 5 | PASS | 5efa4c166493dad9f88ca5a3120c34d4767828a7bd115b310081b3d97f6173a0 | Layout passes. Minor cosmetic note: visible title uses TQ3 0 rather than TQ3_0. |
| WB-04 | 1.3 | 9 | PASS | 7cd48a70d1b4519d37997a1e6dc4f18a7109306ccff0588bc9507556a711a120 | Wide capability, performance and quality tables fit without clipping. |
| WB-05 | 1.3 | 11 | PASS | fc0b99016be099b9e4b7adfc7e892a1004c45f71f3cde628c8321b3440bd80e3 | The 36-pair codec matrix and all later tables remain within page margins. |
| WB-06 | 1.3 | 7 | PASS | 5c80bb0a1ae1065d8aaaf94490d2e6fc41a7c61b085a80b2b514ec1519bc6c89 | Cross-route comparison and claims-control tables are readable and correctly contained. |

## Checks completed

- Every document opened and rendered without a visible corruption or repair artefact.
- Visible workbook versions matched the Stage 4 revisions.
- Text was sharp and readable.
- Tables remained within page margins.
- No rows or columns were visibly clipped.
- No text overlapped another element.
- Revision histories were present and readable.
- No unexplained blank or malformed pages were found.
- Headers and footers were consistently positioned.

## Non-blocking observations

- Long hashes and Git references wrap inside revision-history cells but remain readable.
- Several final pages contain unused lower-page space because their final sections are short.
- WB-03 displays TQ3 0 in the generated page title while the body and identifiers correctly use TQ3_0.

## Gate decision

The Stage 4 visual-layout gate passes for all six generated workbooks.

FAIL-CTRL-001 remains open until the complete repository diff is reviewed and the repair is committed and pushed.

UL-B01-R001 remains Blocked. No llama.cpp build, IBM Granite model load, hardware benchmark, performance run or inference-quality test was performed during this inspection.


## Stage 4B supersession

The semantic diff review on 2026-07-14T16:28:43.839881+00:00 found first-page control-metadata
defects in WB-01 to WB-03. The visual observations remain valid for the earlier
Stage 4 candidate, but the listed WB-01 to WB-03 hashes are superseded.
A fresh visual inspection is required after Stage 4B regeneration.
