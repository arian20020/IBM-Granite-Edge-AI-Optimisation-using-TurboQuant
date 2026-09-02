# Before/after cleanup reconciliation

This report reconciles the frozen Task 1 inventory with the verified Task 14
removal receipt. “Tracked”, “untracked”, and “ignored” below are the
`tracked_status` classifications recorded when `file-inventory.csv` was
created; they are not a claim about a later live `git status`.

## Exact inventory reconciliation

| Frozen status | Before files | Before bytes | Removed files | Removed bytes | After files | After bytes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Tracked | 3,404 | 94,055,555 | 697 | 379,434 | 2,707 | 93,676,121 |
| Untracked | 1,045 | 10,949,857 | 348 | 599,920 | 697 | 10,349,937 |
| Ignored | 608 | 1,872,737,998 | 484 | 17,286,055 | 124 | 1,855,451,943 |
| **Total** | **5,057** | **1,977,743,410** | **1,529** | **18,265,409** | **3,528** | **1,959,478,001** |

Every row satisfies `before - removed = after`. The cleanup removed 1,529 of
5,057 inventoried paths (30.235317%) and 18,265,409 of 1,977,743,410 inventoried
bytes (0.923548%). These are recovery-snapshot reductions, not reductions in
scientific evidence or outcome coverage.

## Disposition of the 1,529 removed paths

| Disposition | Paths | Bytes | Verification |
| --- | ---: | ---: | --- |
| Archived historical material | 1,128 | 2,038,466 | Every destination byte was rehashed; zero entries are unverified. |
| Removed regenerable Python bytecode caches | 401 | 16,226,943 | Exact rule: `scripts/testing/**/__pycache__/*.pyc`; no archive destination. |
| **Total** | **1,529** | **18,265,409** | All selected source paths are absent and receipt-bound. |

The external archive manifest SHA-256 is
`863a13513f760ac70545b67fa4e70e017fe5e0eb89de4313f7c8a68debb54ed7`.
Its receipt SHA-256 is
`f6a661a00783e8e429721de463bceadb88b4136581f2155cbd2d524240c82b6e`,
and the selecting plan SHA-256 is
`9f35b438a11b36d64e959e062fd4791944bfbb2d7105f3c3fad7f393bbe42ef0`.
The local archive location and complete per-entry identities are recorded in
[`archive-summary.json`](archive-summary.json) and its referenced external
`archive-receipt.json`.

## Semantic result after reduction

The byte-identical baseline/final snapshots prove that cleanup retained:

- 169 attempt outcomes: 81 passed, 6 failed, 28 blocked, and 54
  artifact-unavailable;
- 1,857 evidence records, 1,857 unique evidence IDs, and 1,846 unique cited
  evidence paths. The frozen snapshot's legacy `relationship_count=1846` field
  denotes this unique-path cardinality; its `relationships` array has 1,857
  records;
- six Markdown/DOCX report parity pairs;
- six PDFs and all 202 pages;
- the complete guarded comparability matrix and unchanged decision rows.

The six final PDFs were rendered page by page with PyMuPDF and all 202 renders
were visually inspected. Machine checks found zero blank pages, zero clipped
text blocks, and zero render failures. Both portable OpenVINO workbooks were
opened with openpyxl using `read_only=True`, `data_only=False`, and
`keep_links=True`; both had zero machine-absolute paths, zero missing sheet
references, zero external formula references, and zero formula errors.

No claim of scientific improvement, route superiority, licensing permission,
or authorship is implied by this filesystem cleanup.

No semantic evidence regeneration or mutation occurred. Finalization did admit
the pre-existing authoritative EOL representation of a scoped frozen-text set
at the Git boundary using `-text`; this was a byte-preservation correction, not
a change to scientific fields or conclusions.
