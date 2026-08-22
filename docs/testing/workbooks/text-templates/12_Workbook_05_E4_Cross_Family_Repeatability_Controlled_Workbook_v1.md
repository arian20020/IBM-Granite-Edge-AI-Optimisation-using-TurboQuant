# 12 Workbook 05 E4 Cross-Family and Repeatability Controlled Workbook v1.0

**Workbook ID:** `WB-12`
**Controlled filename:** `12_Workbook_05_E4_Cross_Family_Repeatability_Controlled_Workbook_v1.docx`
**Campaign ID:** `GTQ-WB05-MF-v1`
**Stage:** `E4`
**Status:** Initialised - no live result is entered

## Purpose

Resolve key-only, value-only, scalar/Turbo, QJL/Polar and selected cross-family behaviour, then execute ablations, repeatability, regression and unsupported-path gates. This combines completion-plan Tasks R21 and R23 while keeping blocked codecs visible.

## Control boundary

This workbook is a controlled execution structure, not experimental evidence. It may be populated only from independently validated run artifacts. Blank scientific values mean **not measured**; they do not mean zero. A row using an unavailable or failed predecessor remains visible as `Blocked` or `Not applicable` rather than being removed.

| Control | Fixed rule |
| --- | --- |
| Canonical source | docs/testing/workbooks/text-templates/12_Workbook_05_E4_Cross_Family_Repeatability_Controlled_Workbook_v1.md |
| Generated working copy | docs/testing/workbooks/generated/12_Workbook_05_E4_Cross_Family_Repeatability_Controlled_Workbook_v1.docx |
| Execution index | docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv |
| Configuration matrix | docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv |
| Evidence register | docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv |
| Allowed row states | Not started, Blocked pending predecessor, Passed, Failed, Blocked, Not applicable, Skipped by frontier, Infrastructure interrupted |
| Live authority | None until predecessor gates and an explicit self-hosted dispatch are accepted |

## Initial revision record

| Version | Date | Change | Affected scope | Status |
| --- | --- | --- | --- | --- |
| 1.0 | 2026-08-22 | Created the first controlled post-C workbook structure. | E4 | Current - initialised |


[[PAGEBREAK]]

# 1. Entry gate and execution order

| Gate | Required state | Observed state | Evidence |
| --- | --- | --- | --- |
| D2 memory order | Accepted measured ordering |  |  |
| Constituent codec admission | Every codec in a pair admitted and storage-reconciled |  |  |
| 3B common context | Supported by compared configurations |  |  |
| Identity freeze | Same model, request, prompt, rubric and executable hashes where comparison requires it |  |  |

# 2. Controlled coverage

| Test ID | Group | Purpose/reference | Initial status |
| --- | --- | --- | --- |
| OV-TQ-05 | Asymmetric/cross-family | Route A TURBO/U4 key / TURBO/U3 value | Blocked pending predecessor |
| OV-TQ-06 | Asymmetric/cross-family | Route A TURBO/U3 key / TURBO/U4 value | Blocked pending predecessor |
| OV-TQ-07 | Asymmetric/cross-family | Route A Turbo U4 key-only | Blocked pending predecessor |
| OV-TQ-08 | Asymmetric/cross-family | Route A Turbo U4 value-only | Blocked pending predecessor |
| OV-TQ-09 | Asymmetric/cross-family | Route A Turbo U3 key-only | Blocked pending predecessor |
| OV-TQ-10 | Asymmetric/cross-family | Route A Turbo U3 value-only | Blocked pending predecessor |
| OVT-08 | Asymmetric/cross-family | Route B scalar U8 key / TBQ4 value | Blocked pending predecessor |
| OVT-09 | Asymmetric/cross-family | Route B TBQ4 key / scalar U8 value | Blocked pending predecessor |
| OVT-14 | Asymmetric/cross-family | Workbook 05 controlled row OVT-14 | Blocked pending predecessor |
| OVT-15 | Asymmetric/cross-family | Workbook 05 controlled row OVT-15 | Blocked pending predecessor |
| OVT-16 | Asymmetric/cross-family | Workbook 05 controlled row OVT-16 | Blocked pending predecessor |
| OVT-17 | Asymmetric/cross-family | Workbook 05 controlled row OVT-17 | Blocked pending predecessor |
| OVT-18 | Asymmetric/cross-family | Workbook 05 controlled row OVT-18 | Blocked pending predecessor |
| OVT-19 | Asymmetric/cross-family | Workbook 05 controlled row OVT-19 | Blocked pending predecessor |
| OVT-20 | Asymmetric/cross-family | Workbook 05 controlled row OVT-20 | Blocked pending predecessor |
| OVT-21 | Asymmetric/cross-family | Workbook 05 controlled row OVT-21 | Blocked pending predecessor |
| OVT-22 | Asymmetric/cross-family | Workbook 05 controlled row OVT-22 | Blocked pending predecessor |
| OVT-23 | Asymmetric/cross-family | Workbook 05 controlled row OVT-23 | Blocked pending predecessor |
| OVT-24 | Asymmetric/cross-family | Workbook 05 controlled row OVT-24 | Blocked pending predecessor |
| OVT-25 | Asymmetric/cross-family | Workbook 05 controlled row OVT-25 | Blocked pending predecessor |
| OV-TQ-11 | Ablation/repeatability/negative path | Controlled row OV-TQ-11 | Blocked pending predecessor |
| OV-TQ-12 | Ablation/repeatability/negative path | Controlled row OV-TQ-12 | Blocked pending predecessor |
| OV-TQ-15 | Ablation/repeatability/negative path | Controlled row OV-TQ-15 | Blocked pending predecessor |
| OVT-26 | Ablation/repeatability/negative path | Controlled row OVT-26 | Blocked pending predecessor |
| OVT-27 | Ablation/repeatability/negative path | Controlled row OVT-27 | Blocked pending predecessor |
| OVT-28 | Ablation/repeatability/negative path | Controlled row OVT-28 | Blocked pending predecessor |
| OVT-31 | Ablation/repeatability/negative path | Controlled row OVT-31 | Blocked pending predecessor |
| OVT-34 | Ablation/repeatability/negative path | Controlled row OVT-34 | Blocked pending predecessor |
| OVT-35 | Ablation/repeatability/negative path | Controlled row OVT-35 | Blocked pending predecessor |
| OVT-36 | Ablation/repeatability/negative path | Controlled row OVT-36 | Blocked pending predecessor |

# 3. Asymmetric and cross-family result entry

| Test ID/config | K codec/precision | V codec/precision | K query-domain proof | V output-domain proof | Verified K bytes | Verified V bytes | Fallback/output result | Run/evidence | Decision |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |

# 4. Ablation register

| Test/config | Control state | Candidate state | Only variable changed | Metric/quality delta | Activation/storage unchanged? | Decision/evidence |
| --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |

# 5. Repeatability register

| Configuration | Execution identity SHA-256 | Rep 1 | Rep 2 | Rep 3 | Median/range | Stability decision | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |

# 6. Negative and unsupported paths

| Path/capability | Requested behaviour | Expected fail/limit | Observed class | No-fallback/cleanup proof | Affected rows | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| GPU request | Requested device/placement | No GPU claim without proof |  |  |  |  |
| PagedAttention | Alternative cache path | Not applicable unless pinned source exposes it |  |  |  |  |
| Prefill compression | Compress prefill cache | Not assumed |  |  |  |  |
| Non-SDPA | Alternative attention path | Blocked unless explicitly supported |  |  |  |  |
| Unsupported head dimension | Non-reviewed head dimension | Fail securely |  |  |  |  |
| Corrupt checkpoint/evidence | Resume with changed identity | Reject |  |  |  |  |

**E4 acceptance rule:** every required row has a truthful decision and evidence. A blocked constituent codec blocks the combination; it never causes the row to disappear.
