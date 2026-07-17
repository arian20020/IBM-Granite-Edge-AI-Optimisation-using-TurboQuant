# Workbook Revision-Control Validation

**Validation date:** 14 July 2026  
**Branch:** `docs/workbook-revision-control`  
**Pull request:** `#8`  
**Scope:** central revision register, workbook manifest, six embedded histories, reproducible generation and visual rendering

## Result

**PASS — structurally validated.**

This proves that workbook revisions are recorded consistently and reproduced from source-controlled inputs. It does not prove that any Windows, Intel, Granite, backend or codec test passed.

## Validation summary

| Control | Result |
|---|---|
| Append-only register | 15 revision rows retained |
| Current revisions | Exactly one current row for each of six workbooks |
| History depth | Two rows for WB-01–WB-03; three rows for WB-04–WB-06 |
| Current versions | WB-01–WB-03 v1.1; WB-04–WB-06 v1.2 |
| Embedded Word histories | Present with the correct row count in all six DOCX files |
| Change references | Current rows identify branch `docs/workbook-revision-control`, PR #8 and the pending-merge gate |
| Historical references | PR #4 initial releases and PR #5 OpenVINO expansion remain retained |
| Hash validation | Canonical template and generated DOCX hashes match `Controlled-Workbook-Manifest.csv` |
| Reproducibility | A separate generation run produced byte-identical hashes for all six DOCX files |
| Package integrity | All six DOCX ZIP packages passed integrity checks |
| Visual QA | All 46 rendered pages were inspected; no clipping, overlap, broken tables or missing text found |

## Current generated documents

| Workbook | Version | Pages | SHA-256 |
|---|---:|---:|---|
| WB-01 | 1.1 | 6 | `b29721a3ee6b6e5148e3017b9849fc7a0bb339114d59fb3705d4d4f2392037a5` |
| WB-02 | 1.1 | 8 | `f1fbce66cf9465ce835dd1b6c4eee9f9ef0c201071a76170e30d26883aca08fa` |
| WB-03 | 1.1 | 5 | `542b551cf2c4ea7087423bc5eaf2561b0dbf6bdbb39b66ab2f9ccb7529a814e6` |
| WB-04 | 1.2 | 9 | `3d200fcf76332912a232dd180d4068e94612f9a8b7ab20d3913c8457193e7409` |
| WB-05 | 1.2 | 11 | `8a4d383e4629bbfa2bd9702bf2e4b99163a2ce52be8643dce456180e72c15048` |
| WB-06 | 1.2 | 7 | `1698eef4407c67b7dd8c8c4dc913005257d99348309ac27b2b76c96bcefb41a8` |

## Post-merge gate

After PR #8 is merged, the six current rows must replace `Pending merge` with the final merge commit SHA and change `Current - pending merge` to `Current`. This is an intentional approval gate, not missing test evidence.
