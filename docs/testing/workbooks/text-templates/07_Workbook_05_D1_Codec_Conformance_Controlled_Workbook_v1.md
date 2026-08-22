# 07 Workbook 05 D1 Codec Conformance Controlled Workbook v1.0

**Workbook ID:** `WB-07`
**Controlled filename:** `07_Workbook_05_D1_Codec_Conformance_Controlled_Workbook_v1.docx`
**Campaign ID:** `GTQ-WB05-MF-v1`
**Stage:** `D1`
**Status:** Initialised - no live result is entered

## Purpose

Resolve algorithm-level conformance for every admitted codec before any diagnostic or model benchmark. This implements completion-plan Task R17: round trip, packing/unpacking, norm, record size, finite-value boundaries, NaN/Inf policy, deterministic rotation/projection/codebook behaviour, independent K/V dispatch, activation, fallback, allocation formula, repository tests, and bounded quality/perplexity smoke.

## Control boundary

This workbook is a controlled execution structure, not experimental evidence. It may be populated only from independently validated run artifacts. Blank scientific values mean **not measured**; they do not mean zero. A row using an unavailable or failed predecessor remains visible as `Blocked` or `Not applicable` rather than being removed.

| Control | Fixed rule |
| --- | --- |
| Canonical source | docs/testing/workbooks/text-templates/07_Workbook_05_D1_Codec_Conformance_Controlled_Workbook_v1.md |
| Generated working copy | docs/testing/workbooks/generated/07_Workbook_05_D1_Codec_Conformance_Controlled_Workbook_v1.docx |
| Execution index | docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv |
| Configuration matrix | docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv |
| Evidence register | docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv |
| Allowed row states | Not started, Blocked pending predecessor, Passed, Failed, Blocked, Not applicable, Skipped by frontier, Infrastructure interrupted |
| Live authority | None until predecessor gates and an explicit self-hosted dispatch are accepted |

## Initial revision record

| Version | Date | Change | Affected scope | Status |
| --- | --- | --- | --- | --- |
| 1.0 | 2026-08-22 | Created the first controlled post-C workbook structure. | D1 | Current - initialised |


[[PAGEBREAK]]

# 1. Predecessor and identity gate

| Field | Required record | Observed value | Status / evidence |
| --- | --- | --- | --- |
| C1 asset lock | Accepted immutable model/tokenizer/conversion record |  | Blocked pending predecessor |
| C2 harness | Accepted process, watchdog, retry and checkpoint evidence |  | Blocked pending predecessor |
| C3 activation/storage foundation | Request/property/dispatch/fallback/storage schemas accepted |  | Blocked pending predecessor |
| Route A build identity | Exact Runtime and GenAI commits + binary hashes |  | Not started |
| Route B disposition | Admitted, bounded repair, or evidence-backed blocked |  | Not started |

# 2. Codec inventory and expected representation

Expected bytes are planning values from WB-04/WB-05 and must be corrected only from executable evidence.

| Route | Codec | Precision/family | Planning bytes per head at dim 128 | Admission | Verified bytes | Decision |
| --- | --- | --- | --- | --- | --- | --- |
| Route A | SCALAR | U8 | 136 | Pending |  | Not started |
| Route A | SCALAR | U4 | 72 | Pending |  | Not started |
| Route A | TURBO | U4 | 72 | Pending |  | Not started |
| Route A | TURBO | U3 | 52 | Pending |  | Not started |
| Route B | TBQ4 | 4-bit family | 68 | Pending |  | Blocked pending predecessor |
| Route B | TBQ3 | 3-bit family | 52 | Pending |  | Blocked pending predecessor |
| Route B | TBQ4_QJL | QJL 4-bit family | 88 | Pending |  | Blocked pending predecessor |
| Route B | TBQ3_QJL | QJL 3-bit family | 72 | Pending |  | Blocked pending predecessor |
| Route B | POLAR4 | Polar 4-bit family | ~68 | Pending |  | Blocked pending predecessor |
| Route B | POLAR3 | Polar 3-bit family | ~52 | Pending |  | Blocked pending predecessor |

# 3. Conformance execution register

| Execution ID | Route | Controlled test ID | Test boundary | Required evidence | Initial status |
| --- | --- | --- | --- | --- | --- |
| RA-D1-01 | Route A | OV-B08 | Official merged TurboQuant source/API boundary | Source and property inventory | Not started |
| RA-D1-02 | Route A | OV-B09 | Official U3/U4 and independent K/V controls | Pinned source + property proof | Not started |
| RA-D1-03 | Route A | OV-B10 | Official TurboQuant unit/functional diagnostics | Test log + deterministic fixture | Not started |
| RB-D1-01 | Route B | OVT-A01 | Workbook 05 algorithm-level conformance row OVT-A01 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-02 | Route B | OVT-A02 | Workbook 05 algorithm-level conformance row OVT-A02 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-03 | Route B | OVT-A03 | Workbook 05 algorithm-level conformance row OVT-A03 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-04 | Route B | OVT-A04 | Workbook 05 algorithm-level conformance row OVT-A04 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-05 | Route B | OVT-A05 | Workbook 05 algorithm-level conformance row OVT-A05 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-06 | Route B | OVT-A06 | Workbook 05 algorithm-level conformance row OVT-A06 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-07 | Route B | OVT-A07 | Workbook 05 algorithm-level conformance row OVT-A07 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-08 | Route B | OVT-A08 | Workbook 05 algorithm-level conformance row OVT-A08 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-09 | Route B | OVT-A09 | Workbook 05 algorithm-level conformance row OVT-A09 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-10 | Route B | OVT-A10 | Workbook 05 algorithm-level conformance row OVT-A10 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-11 | Route B | OVT-A11 | Workbook 05 algorithm-level conformance row OVT-A11 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |
| RB-D1-12 | Route B | OVT-A12 | Workbook 05 algorithm-level conformance row OVT-A12 | Expected/actual bytes + numerical/dispatch evidence | Blocked pending predecessor |

# 4. Per-codec result entry

| Codec/config | Expected K bytes | Measured K bytes | Expected V bytes | Measured V bytes | Numerical result/tolerance | Dispatch proof | Fallback proof | Run/artifact/evidence | Decision |
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

# 5. D1 acceptance summary

| Codec | Conformance | Verified storage | Dependent rows unblocked? | Failure/blocker code | Evidence path |
| --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  |  |  |  |  |  |
|  |  |  |  |  |  |

**D1 acceptance rule:** every admitted codec has one conformance decision and verified storage. A failed codec blocks only dependent configurations. No unexplained storage discrepancy may advance to D2 or formal comparison.
