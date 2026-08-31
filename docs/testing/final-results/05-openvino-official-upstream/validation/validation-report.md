# Official upstream OpenVINO report validation

## Validation result

Status: **Passed**

The canonical Markdown and generated DOCX are semantically identical according to `workbook-parity.json`. Every repository-local path emitted by the report resolves to an existing artifact, including the verified `05-openvino-official-upstream` route directory. Microsoft Word exported the generated DOCX through the owned-process exporter with a 180-second timeout. The pre-existing Word process baseline remained one process (PID 5032) before and after export; no additional Word process remained.

## PDF structural checks

| Check | Result |
| --- | --- |
| Page count | 52 pages |
| Portrait / landscape | 12 portrait / 40 landscape |
| Non-empty pages | 52 of 52 |
| Searchable title | Passed: `Official upstream OpenVINO final results` |
| Expected audit headings | Passed: attempt ledger, performance, quality, failure, evidence, and revision headings found |
| Markdown-to-DOCX parity | Passed: headings and every table cell match |

## Visual inspection

Every PDF page was rendered to a temporary PNG at 1.5x scale with PyMuPDF. All 52 pages were inspected in numbered contact sheets; title and wide landscape tables were also reviewed at full-page resolution. Availability-table pages 6 and 7 and representative blocked, failed, and passed attempt-ledger pages were reinspected at full resolution after the status-header correction. Temporary PNGs were removed after inspection and were not retained in the repository.

| Finding | Result |
| --- | --- |
| Title page | Clear navy title band; document identity and status summary readable |
| Overflow beyond margins | None observed |
| Clipped table or narrative text | None observed |
| Blank pages | None |
| Orphan headings | None after contextual lead paragraphs were added before landscape-table transitions |
| Repeated table headers | Present on continuation pages |
| Status-table headers | Blue fill with white text, including headers named `Passed`, `Failed`, `Blocked`, and `Artifact unavailable` |
| Status text | Explicit `Passed`, `Failed`, and `Blocked` text present; colour is not the sole signal |
| Status colour | Data-cell Passed is green; Failed is red; Blocked is amber; dark text retains at least 4.5:1 contrast |
| Dense evidence and attempt tables | Landscape, within margins, searchable, and complete |

## Claim and status boundary checks

- The report uses fv2 for all 45 final statuses and fv1 only for observations on the 15 fv2-passed cases.
- The five conversion failures are labelled `Failed`; the 25 hardware-preflight outcomes are labelled `Blocked`, never unavailable.
- Non-passed cases have no fabricated performance or quality observations.
- All measured values, counts, aggregation rules, and evidence hashes are derived from the normalized route bundle.

No blocking visual or semantic issue remains.
