# 13 Workbook 05 F Evidence and Closure Controlled Workbook v1.0

**Workbook ID:** `WB-13`
**Controlled filename:** `13_Workbook_05_F_Evidence_Closure_Controlled_Workbook_v1.docx`
**Campaign ID:** `GTQ-WB05-MF-v1`
**Stage:** `F`
**Status:** Initialised - no live result is entered

## Purpose

Independently validate and ingest completed stage artifacts, populate every controlled workbook/index field, and produce final bounded conclusions. This implements Tasks R24-R26. Closure is impossible while a required row is unresolved or a formal result lacks independent validation.

## Control boundary

This workbook is a controlled execution structure, not experimental evidence. It may be populated only from independently validated run artifacts. Blank scientific values mean **not measured**; they do not mean zero. A row using an unavailable or failed predecessor remains visible as `Blocked` or `Not applicable` rather than being removed.

| Control | Fixed rule |
| --- | --- |
| Canonical source | docs/testing/workbooks/text-templates/13_Workbook_05_F_Evidence_Closure_Controlled_Workbook_v1.md |
| Generated working copy | docs/testing/workbooks/generated/13_Workbook_05_F_Evidence_Closure_Controlled_Workbook_v1.docx |
| Execution index | docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv |
| Configuration matrix | docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv |
| Evidence register | docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv |
| Allowed row states | Not started, Blocked pending predecessor, Passed, Failed, Blocked, Not applicable, Skipped by frontier, Infrastructure interrupted |
| Live authority | None until predecessor gates and an explicit self-hosted dispatch are accepted |

## Initial revision record

| Version | Date | Change | Affected scope | Status |
| --- | --- | --- | --- | --- |
| 1.0 | 2026-08-22 | Created the first controlled post-C workbook structure. | F | Current - initialised |


[[PAGEBREAK]]

# 1. Artifact validation intake

| Stage/batch | Workflow run/attempt | Artifact ID/name | Artifact SHA-256 | Repository head | Manifest validation | Schema/cross-record validation | Secrets/binary scan | Decision/evidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |

# 2. Stage acceptance matrix

| Stage | Current state | Passed | Failed | Blocked/skipped | Accepted artifact/evidence |
| --- | --- | --- | --- | --- | --- |
| C1 | Blocked pending predecessor |  |  |  |  |
| C2 | Blocked pending predecessor |  |  |  |  |
| C3 | Blocked pending predecessor |  |  |  |  |
| C4 | Blocked pending predecessor |  |  |  |  |
| C5 | Blocked pending predecessor |  |  |  |  |
| D1 | Blocked pending predecessor |  |  |  |  |
| D2 | Blocked pending predecessor |  |  |  |  |
| E1 | Blocked pending predecessor |  |  |  |  |
| E2 | Blocked pending predecessor |  |  |  |  |
| E3 | Blocked pending predecessor |  |  |  |  |
| E4 | Blocked pending predecessor |  |  |  |  |

# 3. Post-C workbook completion matrix

| Workbook ID | Stage | Structure state | Unresolved controlled fields | Generated DOCX SHA-256 | Completion decision/evidence |
| --- | --- | --- | --- | --- | --- |
| WB-07 | D1 | Initialised |  |  |  |
| WB-08 | D2 | Initialised |  |  |  |
| WB-09 | E1 | Initialised |  |  |  |
| WB-10 | E2 | Initialised |  |  |  |
| WB-11 | E3 | Initialised |  |  |  |
| WB-12 | E4 | Initialised |  |  |  |
| WB-13 | F | Initialised |  |  |  |

# 4. Execution-index closure

| Stage | Total rows | Passed | Failed | Blocked | Not applicable | Skipped by frontier | Infrastructure interrupted | Unresolved |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| D1 |  |  |  |  |  |  |  |  |
| D2 |  |  |  |  |  |  |  |  |
| E1 |  |  |  |  |  |  |  |  |
| E2 |  |  |  |  |  |  |  |  |
| E3 |  |  |  |  |  |  |  |  |
| E4 |  |  |  |  |  |  |  |  |
| F |  |  |  |  |  |  |  |  |

# 5. Formal result traceability

| Conclusion/result ID | Model/route/config | Metric or quality claim | Matched baseline | Run manifest | Artifact digest | Workbook/index rows | Evidence path | Validation status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |

# 6. Final bounded conclusions

| Decision dimension | Leader/Pareto set | Matched evidence | Limitations/non-claims |
| --- | --- | --- | --- |
| Memory reduction |  |  |  |
| Quality preservation |  |  |  |
| Decode speed |  |  |  |
| Prompt-processing speed |  |  |  |
| TTFT |  |  |  |
| Maximum stable context |  |  |  |
| Stability/repeatability |  |  |  |
| Granite 8B feasibility |  |  |  |
| Granite 30B bounded feasibility |  |  | Feasibility only; no automatic formal performance claim |
| Official merged support boundary |  |  |  |
| Experimental support boundary |  |  |  |
| Recommended application role |  |  |  |

# 7. Final audit checklist

| Audit item | Required result | Observed result | Evidence |
| --- | --- | --- | --- |
| All controlled rows resolved | No blank/unresolved status |  |  |
| All formal results independently validated | Accepted artifact and exact run manifest |  |  |
| Failures/skips remain visible | No deleted inconvenient row |  |  |
| Traceability | Every conclusion resolves to workbook/index/run/artifact evidence |  |  |
| Payload safety | No secret, model, source tree, executable, wheel or archive committed as evidence |  |  |
| Support language | No unsupported codec/device/model claim |  |  |
| Generated documents | Templates, DOCX files and manifests regenerated and hashed |  |  |
| Review lifecycle | All PR threads resolved and explicit project-owner approval obtained |  |  |

# 8. Closure decision

| Pack status | Scientific results authorised? | Workbook closure authorised? | Decision owner/date | Evidence |
| --- | --- | --- | --- | --- |
| Initialised | false | false |  | No live post-C result has been accepted |

**F acceptance rule:** every controlled row is resolved, every formal result is independently validated, all generated workbooks/manifests are regenerated and hashed, and the final results PR is reviewed. Until then the closure decision remains open.
