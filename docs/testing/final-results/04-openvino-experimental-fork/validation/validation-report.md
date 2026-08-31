# Experimental OpenVINO fork report validation

## Validation result

Status: **Passed**

The canonical Markdown and generated DOCX are semantically identical according to `workbook-parity.json`. Every repository-local path emitted by the report resolves to an existing artifact, including the verified `04-openvino-experimental-fork` route directory. Microsoft Word exported the generated DOCX through the owned-process exporter with a 180-second timeout. The pre-existing Word process baseline remained one process (PID 5032) before and after export; no additional Word process remained.

## PDF structural checks

| Check | Result |
| --- | --- |
| Page count | 54 pages |
| Portrait / landscape | 12 portrait / 42 landscape |
| Non-empty pages | 54 of 54 |
| Searchable title | Passed: `Experimental OpenVINO fork final results` |
| Expected audit headings | Passed: attempt ledger, performance, quality, failure, evidence, and revision headings found |
| Markdown-to-DOCX parity | Passed: headings and every table cell match |

## Visual inspection

Every PDF page was rendered to a temporary PNG at 1.5x scale with PyMuPDF. All 54 pages were inspected in numbered contact sheets; title and wide landscape tables were also reviewed at full-page resolution. Availability-table pages 6 and 7 and representative attempt-ledger pages were reinspected at full resolution after the status-header correction. Temporary PNGs were removed after inspection and were not retained in the repository.

| Finding | Result |
| --- | --- |
| Title page | Clear navy title band; document identity and status summary readable |
| Overflow beyond margins | None observed |
| Clipped table or narrative text | None observed |
| Blank pages | None |
| Orphan headings | None after contextual lead paragraphs were added before landscape-table transitions |
| Repeated table headers | Present on continuation pages |
| Status-table headers | Blue fill with white text, including headers named `Passed`, `Failed`, `Blocked`, and `Artifact unavailable` |
| Status text | Explicit `Passed` and `Artifact unavailable` text present; colour is not the sole signal |
| Status colour | Data-cell Passed is green; data-cell Artifact unavailable is neutral grey; dark text retains at least 4.5:1 contrast |
| Dense evidence and attempt tables | Landscape, within margins, searchable, and complete |

## Claim and status boundary checks

- The report uses fv6 campaign identity and accounts for all 81 normalized attempts.
- The 54 cases lacking validated model artifacts are labelled `Artifact unavailable`, not failed or blocked.
- Performance and objective quality values appear only for the 27 passed configurations.
- All measured values, counts, aggregation rules, and evidence hashes are derived from the normalized route bundle.

No blocking visual or semantic issue remains.
