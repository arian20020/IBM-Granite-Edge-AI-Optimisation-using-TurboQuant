# 11 Workbook 05 E3 Granite 30B Bounded Feasibility Controlled Workbook v1.0

**Workbook ID:** `WB-11`
**Controlled filename:** `11_Workbook_05_E3_Granite_30B_Bounded_Feasibility_Controlled_Workbook_v1.docx`
**Campaign ID:** `GTQ-WB05-MF-v1`
**Stage:** `E3`
**Status:** Initialised - no live result is entered

## Purpose

Execute the project-owner-approved Granite 30B bounded-feasibility extension in lowest-weight-first order. This extension was approved after the older R19-R23 plan. It is deliberately separate from the formal 3B campaign: its outcome may be Feasible, Bounded, or Blocked, and it cannot authorise formal performance statistics.

## Control boundary

This workbook is a controlled execution structure, not experimental evidence. It may be populated only from independently validated run artifacts. Blank scientific values mean **not measured**; they do not mean zero. A row using an unavailable or failed predecessor remains visible as `Blocked` or `Not applicable` rather than being removed.

| Control | Fixed rule |
| --- | --- |
| Canonical source | docs/testing/workbooks/text-templates/11_Workbook_05_E3_Granite_30B_Bounded_Feasibility_Controlled_Workbook_v1.md |
| Generated working copy | docs/testing/workbooks/generated/11_Workbook_05_E3_Granite_30B_Bounded_Feasibility_Controlled_Workbook_v1.docx |
| Execution index | docs/testing/Workbook-05-Post-C-Execution-Index-v1.csv |
| Configuration matrix | docs/testing/Workbook-05-Post-C-Configuration-Matrix-v1.csv |
| Evidence register | docs/testing/Workbook-05-Post-C-Evidence-Register-v1.csv |
| Allowed row states | Not started, Blocked pending predecessor, Passed, Failed, Blocked, Not applicable, Skipped by frontier, Infrastructure interrupted |
| Live authority | None until predecessor gates and an explicit self-hosted dispatch are accepted |

## Initial revision record

| Version | Date | Change | Affected scope | Status |
| --- | --- | --- | --- | --- |
| 1.0 | 2026-08-22 | Created the first controlled post-C workbook structure. | E3 | Current - initialised |


[[PAGEBREAK]]

# 1. Extension identity and non-claims

| Field | Required record | Observed value | Status |
| --- | --- | --- | --- |
| Model family/size | IBM Granite 4.1 30B candidate selected by accepted asset process |  | Not started |
| Exact model repository/revision | Immutable full revision and file catalogue |  | Not started |
| Weight candidate | Lowest accepted retained format first; no unreviewed format |  | Not started |
| KV candidate | Derived from D2 verified memory order |  | Blocked pending predecessor |
| Formal performance | Forbidden in this workbook | false | Fixed |
| Formal quality | Forbidden unless a later separately approved package is created | false | Fixed |

# 2. Candidate order

| Candidate ID | Weight selection rule | KV selection rule | Admission rationale | Resolved asset/config | Status |
| --- | --- | --- | --- | --- | --- |
| E3-CANDIDATE-01 | Lowest accepted weight precision | D2 verified memory rank 1 | Lowest-weight + lowest-KV first |  | Blocked pending predecessor |
| E3-CANDIDATE-02 | Lowest accepted weight precision | D2 verified memory rank 2 | Next-lowest verified KV candidate |  | Blocked pending predecessor |
| E3-CANDIDATE-03 | Lowest accepted weight precision | RA-SCALAR-U4-SYM | Standard-cache comparison only if preflight is safe |  | Blocked pending predecessor |
| E3-CANDIDATE-04 | Next accepted weight precision | D2 verified memory rank 1 | Escalation only after candidate 1 completes safely |  | Blocked pending predecessor |

# 3. Conservative bounded attempt ladder

Each candidate begins at 512 tokens. Higher points run only after a clean lower point and healthy cooldown. Preflight may block before model load. The same accepted watchdog and descendant cleanup apply.

| Candidate | Context | Attempt | Initial status | Preflight | Load/generation | Safety/cleanup | Run/artifact/evidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| E3-CANDIDATE-01 | 512 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-01 | 1024 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-01 | 2048 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-01 | 4096 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-02 | 512 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-02 | 1024 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-02 | 2048 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-02 | 4096 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-03 | 512 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-03 | 1024 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-03 | 2048 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-03 | 4096 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-04 | 512 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-04 | 1024 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-04 | 2048 | 0 | Blocked pending predecessor |  |  |  |  |
| E3-CANDIDATE-04 | 4096 | 0 | Blocked pending predecessor |  |  |  |  |

# 4. Resource boundary

| Candidate/context | Estimated asset bytes | Free disk | Available RAM | Commit % | Peak private MB | Min available RAM | Stop class | Evidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |  |  |

# 5. Bounded feasibility decision

| Candidate | Decision: Feasible/Bounded/Blocked | Last completed boundary | First failure/blocker | Reason | What this does not prove | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
|  |  |  |  |  | No formal speed, quality or maximum-context claim |  |
|  |  |  |  |  | No formal speed, quality or maximum-context claim |  |
|  |  |  |  |  | No formal speed, quality or maximum-context claim |  |
|  |  |  |  |  | No formal speed, quality or maximum-context claim |  |

**E3 acceptance rule:** the candidate order is honoured; no higher-memory candidate runs after a lower candidate establishes an evidence-backed safety block. All rows keep `Formal_Statistics_Allowed=false`.
