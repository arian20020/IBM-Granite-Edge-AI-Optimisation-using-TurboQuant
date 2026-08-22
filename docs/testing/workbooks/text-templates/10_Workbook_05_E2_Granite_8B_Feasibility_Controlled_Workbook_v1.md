# 10 Workbook 05 E2 Granite 8B Feasibility Controlled Workbook v1.0

**Workbook ID:** `WB-10`
**Controlled filename:** `10_Workbook_05_E2_Granite_8B_Feasibility_Controlled_Workbook_v1.docx`
**Campaign ID:** `GTQ-WB05-MF-v1`
**Stage:** `E2`
**Status:** Initialised - no live result is entered

## Purpose

Determine whether the controlled 15.7 GiB laptop can use Granite 8B with the standard control and the most decision-relevant compressed candidates. This implements Task R22. Discovery-only marker success is not formal quality evidence.

## Control boundary

This workbook is a controlled execution structure, not experimental evidence. It may be populated only from independently validated run artifacts. Blank scientific values mean **not measured**; they do not mean zero. A row using an unavailable or failed predecessor remains visible as `Blocked` or `Not applicable` rather than being removed.

| Control | Fixed rule |
| --- | --- |
| Canonical source | docs/testing/workbooks/text-templates/10_Workbook_05_E2_Granite_8B_Feasibility_Controlled_Workbook_v1.md |
| Generated working copy | docs/testing/workbooks/generated/10_Workbook_05_E2_Granite_8B_Feasibility_Controlled_Workbook_v1.docx |
| Execution index | docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv |
| Configuration matrix | docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv |
| Evidence register | docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv |
| Allowed row states | Not started, Blocked pending predecessor, Passed, Failed, Blocked, Not applicable, Skipped by frontier, Infrastructure interrupted |
| Live authority | None until predecessor gates and an explicit self-hosted dispatch are accepted |

## Initial revision record

| Version | Date | Change | Affected scope | Status |
| --- | --- | --- | --- | --- |
| 1.0 | 2026-08-22 | Created the first controlled post-C workbook structure. | E2 | Current - initialised |


[[PAGEBREAK]]

# 1. Entry and safety gate

| Gate | Required state | Observed/evidence | Decision |
| --- | --- | --- | --- |
| Granite 8B immutable asset | Exact repository/revision/model/tokenizer/conversion hashes |  | Blocked pending predecessor |
| 3B evidence | E1 accepted and candidate roles known |  | Blocked pending predecessor |
| Candidate quality | Only quality-valid decision candidates promoted |  |  |
| Machine preflight | No conflicting process; accepted RAM/commit/disk thresholds |  |  |
| Harness | Same watchdog, retry, cooldown and cleanup as E1 |  |  |

# 2. Candidate promotion register

| Order | Configuration | Promotion reason | D2 memory rank | E1 quality decision | Promoted? | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| E2-01 | RA-TURBO-U3-SYM | Lowest-memory Route A candidate |  |  |  |  |
| E2-02 | RA-TURBO-U4-SYM | Decision-relevant Route A candidate |  |  |  |  |
| E2-03 | RA-SCALAR-U4-SYM | Aggressive standard control |  |  |  |  |
| E2-04 | RA-SCALAR-U8-SYM | Practical standard control |  |  |  |  |
| E2-05 | RB-TBQ3-SYM | Route B candidate if admitted and quality-valid |  |  |  |  |
| E2-06 | RB-QJL3-SYM | QJL candidate if admitted and quality-valid |  |  |  |  |
| E2-07 | RB-POLAR3-SYM | Polar candidate if admitted and quality-valid |  |  |  |  |

# 3. 8B frontier attempts

| Candidate | Configuration | Context | Attempt | Initial status | Marker/output integrity | Safety/exit | Run/artifact/evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| E2-01 | RA-TURBO-U3-SYM | 512 | 0 | Blocked pending predecessor |  |  |  |
| E2-01 | RA-TURBO-U3-SYM | 1024 | 0 | Blocked pending predecessor |  |  |  |
| E2-01 | RA-TURBO-U3-SYM | 2048 | 0 | Blocked pending predecessor |  |  |  |
| E2-01 | RA-TURBO-U3-SYM | 4096 | 0 | Blocked pending predecessor |  |  |  |
| E2-01 | RA-TURBO-U3-SYM | 8192 | 0 | Blocked pending predecessor |  |  |  |
| E2-02 | RA-TURBO-U4-SYM | 512 | 0 | Blocked pending predecessor |  |  |  |
| E2-02 | RA-TURBO-U4-SYM | 1024 | 0 | Blocked pending predecessor |  |  |  |
| E2-02 | RA-TURBO-U4-SYM | 2048 | 0 | Blocked pending predecessor |  |  |  |
| E2-02 | RA-TURBO-U4-SYM | 4096 | 0 | Blocked pending predecessor |  |  |  |
| E2-02 | RA-TURBO-U4-SYM | 8192 | 0 | Blocked pending predecessor |  |  |  |
| E2-03 | RA-SCALAR-U4-SYM | 512 | 0 | Blocked pending predecessor |  |  |  |
| E2-03 | RA-SCALAR-U4-SYM | 1024 | 0 | Blocked pending predecessor |  |  |  |
| E2-03 | RA-SCALAR-U4-SYM | 2048 | 0 | Blocked pending predecessor |  |  |  |
| E2-03 | RA-SCALAR-U4-SYM | 4096 | 0 | Blocked pending predecessor |  |  |  |
| E2-03 | RA-SCALAR-U4-SYM | 8192 | 0 | Blocked pending predecessor |  |  |  |
| E2-04 | RA-SCALAR-U8-SYM | 512 | 0 | Blocked pending predecessor |  |  |  |
| E2-04 | RA-SCALAR-U8-SYM | 1024 | 0 | Blocked pending predecessor |  |  |  |
| E2-04 | RA-SCALAR-U8-SYM | 2048 | 0 | Blocked pending predecessor |  |  |  |
| E2-04 | RA-SCALAR-U8-SYM | 4096 | 0 | Blocked pending predecessor |  |  |  |
| E2-04 | RA-SCALAR-U8-SYM | 8192 | 0 | Blocked pending predecessor |  |  |  |
| E2-05 | RB-TBQ3-SYM | 512 | 0 | Blocked pending predecessor |  |  |  |
| E2-05 | RB-TBQ3-SYM | 1024 | 0 | Blocked pending predecessor |  |  |  |
| E2-05 | RB-TBQ3-SYM | 2048 | 0 | Blocked pending predecessor |  |  |  |
| E2-05 | RB-TBQ3-SYM | 4096 | 0 | Blocked pending predecessor |  |  |  |
| E2-05 | RB-TBQ3-SYM | 8192 | 0 | Blocked pending predecessor |  |  |  |
| E2-06 | RB-QJL3-SYM | 512 | 0 | Blocked pending predecessor |  |  |  |
| E2-06 | RB-QJL3-SYM | 1024 | 0 | Blocked pending predecessor |  |  |  |
| E2-06 | RB-QJL3-SYM | 2048 | 0 | Blocked pending predecessor |  |  |  |
| E2-06 | RB-QJL3-SYM | 4096 | 0 | Blocked pending predecessor |  |  |  |
| E2-06 | RB-QJL3-SYM | 8192 | 0 | Blocked pending predecessor |  |  |  |
| E2-07 | RB-POLAR3-SYM | 512 | 0 | Blocked pending predecessor |  |  |  |
| E2-07 | RB-POLAR3-SYM | 1024 | 0 | Blocked pending predecessor |  |  |  |
| E2-07 | RB-POLAR3-SYM | 2048 | 0 | Blocked pending predecessor |  |  |  |
| E2-07 | RB-POLAR3-SYM | 4096 | 0 | Blocked pending predecessor |  |  |  |
| E2-07 | RB-POLAR3-SYM | 8192 | 0 | Blocked pending predecessor |  |  |  |

# 4. Resource and placement record

| Candidate/context | Load result | Actual device | Model placement | KV placement | Peak private MB | Min available RAM MB | Max commit % | Watchdog class | Cleanup |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |  |

# 5. Feasibility decision

| Configuration | Decision: Feasible/Bounded/Blocked | Last stable context | First repeated failure or limit | Quality scope actually tested | Performance scope actually tested | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |

**E2 acceptance rule:** each promoted candidate is classified `Feasible`, `Bounded`, or `Blocked` from real evidence. A discovery-only exact-marker pass must state that full 8B quality and formal performance remain unproven.
