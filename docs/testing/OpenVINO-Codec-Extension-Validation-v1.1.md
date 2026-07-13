# OpenVINO Codec Extension Validation v1.1

**Validation date:** 13 July 2026  
**Scope:** workbook structure, test coverage, traceability and generated-document layout.  
**Hardware execution:** not yet performed.

## Validated coverage

- WB-04 contains 12 official Turbo/scalar capability combinations and 20 formal official TurboQuant tests.
- WB-05 contains 12 algorithm-conformance tests, all 36 ordered K/V combinations across six experimental codecs and 36 formal end-to-end/ablation/context tests.
- WB-06 contains official TBQ3/TBQ4 and experimental TBQ, QJL and PolarQuant comparison rows.
- The extension traceability file contains 119 unique added OpenVINO IDs.
- The complete controlled campaign contains 224 unique test IDs when the 105-ID source baseline and 119-ID extension are combined.
- Generated DOCX workbooks were rendered and visually inspected: WB-04 9 pages, WB-05 10 pages and WB-06 6 pages.
- DOCX generation was repeated independently and produced identical hashes.

## Validated generated hashes

| Workbook | SHA-256 |
|---|---|
| WB-04 | `0ff0af6b93a71de52cf7a17ce853bfe67ccbb05c91de62b0f560271c36075c83` |
| WB-05 | `04cb2f3d2582c3b8bbd2f5d1a79bb7a8347552ac7a7e4b75f5f56b8af58f0c22` |
| WB-06 | `83ca79bc42db7d0a0e390ccadb4eb234f58776c33bae363c096a0075aa0aa27d` |

## Honest boundary

This validation does not prove that any codec builds, activates or benefits Granite on the target Intel laptop. Those claims require pinned source and model revisions, actual device/backend evidence, expected cache allocation, raw outputs, memory/performance measurements, quality evaluation and repeated successful runs.

Run both validators after pulling the branch:

```powershell
.\scripts\testing\Validate-Controlled-Testing-Workspace.ps1
.\scripts\testing\Validate-OpenVINO-Codec-Extension.ps1
```
