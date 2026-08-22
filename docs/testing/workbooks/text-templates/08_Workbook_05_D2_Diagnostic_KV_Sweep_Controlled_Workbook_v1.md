# 08 Workbook 05 D2 Diagnostic K/V Sweep Controlled Workbook v1.0

**Workbook ID:** `WB-08`
**Controlled filename:** `08_Workbook_05_D2_Diagnostic_KV_Sweep_Controlled_Workbook_v1.docx`
**Campaign ID:** `GTQ-WB05-MF-v1`
**Stage:** `D2`
**Status:** Initialised - no live result is entered

## Purpose

Lock the measured low-memory-to-high-memory execution order and run every admitted ordered K/V pair. This implements Task R18. Rank is recalculated from measured K bytes plus measured V bytes; ties use symmetric first, lower K bytes, lower V bytes, then stable codec name.

## Control boundary

This workbook is a controlled execution structure, not experimental evidence. It may be populated only from independently validated run artifacts. Blank scientific values mean **not measured**; they do not mean zero. A row using an unavailable or failed predecessor remains visible as `Blocked` or `Not applicable` rather than being removed.

| Control | Fixed rule |
| --- | --- |
| Canonical source | docs/testing/workbooks/text-templates/08_Workbook_05_D2_Diagnostic_KV_Sweep_Controlled_Workbook_v1.md |
| Generated working copy | docs/testing/workbooks/generated/08_Workbook_05_D2_Diagnostic_KV_Sweep_Controlled_Workbook_v1.docx |
| Execution index | docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv |
| Configuration matrix | docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv |
| Evidence register | docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv |
| Allowed row states | Not started, Blocked pending predecessor, Passed, Failed, Blocked, Not applicable, Skipped by frontier, Infrastructure interrupted |
| Live authority | None until predecessor gates and an explicit self-hosted dispatch are accepted |

## Initial revision record

| Version | Date | Change | Affected scope | Status |
| --- | --- | --- | --- | --- |
| 1.0 | 2026-08-22 | Created the first controlled post-C workbook structure. | D2 | Current - initialised |


[[PAGEBREAK]]

# 1. D2 entry gate

| Gate | Required state | Observed state | Evidence |
| --- | --- | --- | --- |
| D1 codec conformance | Every codec Passed, Failed, Blocked, or Not applicable |  |  |
| Storage measurements | Verified K/V bytes for every admitted codec |  |  |
| Diagnostic model | Immutable asset lock and successful standard baseline |  |  |
| Harness | C2 accepted; watchdog/cooldown/retry enabled |  |  |

# 2. Route A ordered-pair plan

| Execution ID | Route | Test ID | K algorithm | K precision | V algorithm | V precision | Initial status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| D2-RA-01 | Route A | OV-TQS-01 | TURBO | U4 | TURBO | U4 | Blocked pending predecessor |
| D2-RA-02 | Route A | OV-TQS-02 | TURBO | U3 | TURBO | U3 | Blocked pending predecessor |
| D2-RA-03 | Route A | OV-TQS-03 | TURBO | U4 | TURBO | U3 | Blocked pending predecessor |
| D2-RA-04 | Route A | OV-TQS-04 | TURBO | U3 | TURBO | U4 | Blocked pending predecessor |
| D2-RA-05 | Route A | OV-TQS-05 | TURBO | U4 | SCALAR | U8 | Blocked pending predecessor |
| D2-RA-06 | Route A | OV-TQS-06 | SCALAR | U8 | TURBO | U4 | Blocked pending predecessor |
| D2-RA-07 | Route A | OV-TQS-07 | TURBO | U3 | SCALAR | U8 | Blocked pending predecessor |
| D2-RA-08 | Route A | OV-TQS-08 | SCALAR | U8 | TURBO | U3 | Blocked pending predecessor |
| D2-RA-09 | Route A | OV-TQS-09 | TURBO | U4 | SCALAR | U4 | Blocked pending predecessor |
| D2-RA-10 | Route A | OV-TQS-10 | SCALAR | U4 | TURBO | U4 | Blocked pending predecessor |
| D2-RA-11 | Route A | OV-TQS-11 | TURBO | U3 | SCALAR | U4 | Blocked pending predecessor |
| D2-RA-12 | Route A | OV-TQS-12 | SCALAR | U4 | TURBO | U3 | Blocked pending predecessor |

# 3. Route B 6 x 6 ordered-pair plan

| Execution ID | Route | Test ID | K codec | Planning K bytes | V codec | Planning V bytes | Initial status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| D2-RB-01 | Route B | OVT-S01 | TBQ4 | 68 | TBQ4 | 68 | Blocked pending predecessor |
| D2-RB-02 | Route B | OVT-S02 | TBQ4 | 68 | TBQ3 | 52 | Blocked pending predecessor |
| D2-RB-03 | Route B | OVT-S03 | TBQ4 | 68 | TBQ4_QJL | 88 | Blocked pending predecessor |
| D2-RB-04 | Route B | OVT-S04 | TBQ4 | 68 | TBQ3_QJL | 72 | Blocked pending predecessor |
| D2-RB-05 | Route B | OVT-S05 | TBQ4 | 68 | POLAR4 | ~68 | Blocked pending predecessor |
| D2-RB-06 | Route B | OVT-S06 | TBQ4 | 68 | POLAR3 | ~52 | Blocked pending predecessor |
| D2-RB-07 | Route B | OVT-S07 | TBQ3 | 52 | TBQ4 | 68 | Blocked pending predecessor |
| D2-RB-08 | Route B | OVT-S08 | TBQ3 | 52 | TBQ3 | 52 | Blocked pending predecessor |
| D2-RB-09 | Route B | OVT-S09 | TBQ3 | 52 | TBQ4_QJL | 88 | Blocked pending predecessor |
| D2-RB-10 | Route B | OVT-S10 | TBQ3 | 52 | TBQ3_QJL | 72 | Blocked pending predecessor |
| D2-RB-11 | Route B | OVT-S11 | TBQ3 | 52 | POLAR4 | ~68 | Blocked pending predecessor |
| D2-RB-12 | Route B | OVT-S12 | TBQ3 | 52 | POLAR3 | ~52 | Blocked pending predecessor |
| D2-RB-13 | Route B | OVT-S13 | TBQ4_QJL | 88 | TBQ4 | 68 | Blocked pending predecessor |
| D2-RB-14 | Route B | OVT-S14 | TBQ4_QJL | 88 | TBQ3 | 52 | Blocked pending predecessor |
| D2-RB-15 | Route B | OVT-S15 | TBQ4_QJL | 88 | TBQ4_QJL | 88 | Blocked pending predecessor |
| D2-RB-16 | Route B | OVT-S16 | TBQ4_QJL | 88 | TBQ3_QJL | 72 | Blocked pending predecessor |
| D2-RB-17 | Route B | OVT-S17 | TBQ4_QJL | 88 | POLAR4 | ~68 | Blocked pending predecessor |
| D2-RB-18 | Route B | OVT-S18 | TBQ4_QJL | 88 | POLAR3 | ~52 | Blocked pending predecessor |
| D2-RB-19 | Route B | OVT-S19 | TBQ3_QJL | 72 | TBQ4 | 68 | Blocked pending predecessor |
| D2-RB-20 | Route B | OVT-S20 | TBQ3_QJL | 72 | TBQ3 | 52 | Blocked pending predecessor |
| D2-RB-21 | Route B | OVT-S21 | TBQ3_QJL | 72 | TBQ4_QJL | 88 | Blocked pending predecessor |
| D2-RB-22 | Route B | OVT-S22 | TBQ3_QJL | 72 | TBQ3_QJL | 72 | Blocked pending predecessor |
| D2-RB-23 | Route B | OVT-S23 | TBQ3_QJL | 72 | POLAR4 | ~68 | Blocked pending predecessor |
| D2-RB-24 | Route B | OVT-S24 | TBQ3_QJL | 72 | POLAR3 | ~52 | Blocked pending predecessor |
| D2-RB-25 | Route B | OVT-S25 | POLAR4 | ~68 | TBQ4 | 68 | Blocked pending predecessor |
| D2-RB-26 | Route B | OVT-S26 | POLAR4 | ~68 | TBQ3 | 52 | Blocked pending predecessor |
| D2-RB-27 | Route B | OVT-S27 | POLAR4 | ~68 | TBQ4_QJL | 88 | Blocked pending predecessor |
| D2-RB-28 | Route B | OVT-S28 | POLAR4 | ~68 | TBQ3_QJL | 72 | Blocked pending predecessor |
| D2-RB-29 | Route B | OVT-S29 | POLAR4 | ~68 | POLAR4 | ~68 | Blocked pending predecessor |
| D2-RB-30 | Route B | OVT-S30 | POLAR4 | ~68 | POLAR3 | ~52 | Blocked pending predecessor |
| D2-RB-31 | Route B | OVT-S31 | POLAR3 | ~52 | TBQ4 | 68 | Blocked pending predecessor |
| D2-RB-32 | Route B | OVT-S32 | POLAR3 | ~52 | TBQ3 | 52 | Blocked pending predecessor |
| D2-RB-33 | Route B | OVT-S33 | POLAR3 | ~52 | TBQ4_QJL | 88 | Blocked pending predecessor |
| D2-RB-34 | Route B | OVT-S34 | POLAR3 | ~52 | TBQ3_QJL | 72 | Blocked pending predecessor |
| D2-RB-35 | Route B | OVT-S35 | POLAR3 | ~52 | POLAR4 | ~68 | Blocked pending predecessor |
| D2-RB-36 | Route B | OVT-S36 | POLAR3 | ~52 | POLAR3 | ~52 | Blocked pending predecessor |

# 4. Measured capability and rank register

| Test ID | Verified K bytes | Verified V bytes | Total bytes | Tie-break key | Memory rank | Dispatch | Fallback | Output integrity | Run/artifact/evidence | Final status |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |  |

[[PAGEBREAK]]

# 5. Blocker propagation

| Blocked codec | Root decision/evidence | Dependent test IDs | Applied status | Reason |
| --- | --- | --- | --- | --- |
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |
|  |  |  |  |  |

**D2 acceptance rule:** all 48 planned pairs are explicitly `Passed`, `Failed`, `Blocked`, or `Not applicable`; no pair remains unresolved. The execution index receives verified bytes, rank, frontier status, run ID and evidence path.
