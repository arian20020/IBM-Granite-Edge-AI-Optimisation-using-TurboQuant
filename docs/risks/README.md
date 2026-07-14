# Risk, Assumption, Constraint and Licence Control

**Document ID:** IDX-RACL-001  
**Version:** 0.3  
**Status:** Populated working control area — formal review and baseline pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Formal scope, risk, assumption, constraint and licence gate review  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`

## 1. Purpose

This directory is the single controlled home for the project’s risk, assumption, constraint and licence records.

The four concepts are kept in separate registers because they are reviewed and resolved differently:

- a **risk** is an uncertain event or condition that may affect the project;
- an **assumption** is something currently treated as true but requiring evidence;
- a **constraint** is a boundary the project must work within;
- a **licence record** documents whether a third-party component, model, dataset, asset or source may be used, modified, packaged or redistributed.

The registers together support `G-M05`, `PD-05` and `EP-007`. Populating the files makes the control area operational, but does not by itself prove that every entry has been validated or that those tasks are Verified.

## 2. Authoritative registers and supporting controls

| Record type | ID prefix | Authoritative file | Current content state | Main outcome |
|---|---|---|---|---|
| Risk | `R-` | [Risk Register](Risk-Register.md) | Two full draft records and a complete candidate backlog requiring consolidation | Open, Monitoring, Triggered, Accepted, Closed or Superseded |
| Assumption | `A-` | [Assumption Register](Assumption-Register.md) | Core assumptions populated; evidence review pending | Pending, Confirmed, Rejected or Superseded |
| Constraint | `C-` | [Constraint Register](Constraint-Register.md) | Important project constraints populated; source and scope review pending | Active, Changed, Removed or Superseded |
| Licence | `L-` | [Licence Register](Licence-Register.md) | Key components and materials populated; exact-version and packaging review pending | Pending, Approved, Restricted, Rejected or Superseded |

Supporting control files:

- [Control and Validation Plan](Control-and-Validation-Plan.md) — defines the order, scoring method, review gates and Definition of Done;
- [Review Log](Review-Log.md) — records formal reviews, decisions and open actions;
- [Candidate Risk Backlog](candidates/README.md) — contains the unassessed project-wide risk inventory;
- [Baseline directory](baselines/README.md) — stores reviewed frozen snapshots after approval;
- [Baseline Record Template](baselines/Baseline-Record-Template.md) — controls future baseline metadata and approval checks.

## 3. Directory structure

```text
docs/risks/
├── README.md
├── Control-and-Validation-Plan.md
├── Risk-Register.md
├── Assumption-Register.md
├── Constraint-Register.md
├── Licence-Register.md
├── Review-Log.md
├── candidates/
│   ├── README.md
│   ├── 01-project-scope-windows.md
│   ├── 02-openvino-turboquant.md
│   ├── 03-turbovec-hardware-performance.md
│   ├── 04-security-ux-ai-quality.md
│   ├── 05-testing-evidence.md
│   └── 06-licence-release.md
└── baselines/
    ├── README.md
    └── Baseline-Record-Template.md
```

## 4. Common control rules

1. Use stable IDs and do not reuse an ID for a different item.
2. Keep one authoritative record and cross-reference it instead of duplicating the same entry in several files.
3. Preserve rejected assumptions, closed risks, removed constraints and rejected licences so the decision history remains visible.
4. Link each item to the requirement, work package, experiment, ADR, test, issue, pull request or external controlled evidence that supports it.
5. Record an owner, current status, last-review date and next-review date.
6. Do not claim that an assumption is confirmed or a licence is approved without reviewable evidence.
7. Do not treat a listed risk as controlled until its required treatment fields are complete.
8. Do not delete negative findings, blockers or restrictions.
9. Do not commit API keys, personal data, restricted model weights, unlicensed material or confidential third-party content.
10. Use relative repository links where possible and authoritative external links for licences and model cards.
11. Record material changes through project change control.
12. Keep requested settings, actual runtime behaviour, published claims, estimates and measured results separate.

## 5. Record-type decision guide

| Statement form | Register |
|---|---|
| Something may happen and affect the project | Risk Register |
| Something is currently believed and needs evidence | Assumption Register |
| Something is a fixed boundary the project must obey | Constraint Register |
| Something concerns permission to use, modify or distribute external material | Licence Register |
| Something has already failed or occurred | Failure Register, issue or incident record; retain a linked risk only when recurrence or continuing impact remains |
| Something states what the system must do | Requirements and RTM, not these registers |

## 6. Review cycle

The live registers are reviewed:

- weekly during active development;
- before a major implementation or experiment gate that depends on an entry;
- when new contradictory evidence is discovered;
- when a third-party version, model, runtime, dataset or licence changes;
- before packaging or distributing a release;
- before freezing a working, supervisor-reviewed or final baseline.

Each formal review must be added to [Review-Log.md](Review-Log.md).

## 7. Baseline and freeze rule

A baseline is a reviewed snapshot of the four live registers at a defined point in time. Freezing a baseline does not mean the live registers can never change.

```text
Update the live registers
        ↓
Follow the Control and Validation Plan
        ↓
Record a formal review
        ↓
Freeze an approved snapshot
        ↓
Continue maintaining the live registers
        ↓
Freeze a later version when required
```

No `v1.0` baseline is claimed until the freeze criteria in [baselines/README.md](baselines/README.md) are satisfied.

## 8. Completion and validation boundary

The control area is now **populated and operational**, but formal review remains.

The related tasks may move to **Implemented** only when the controlled Definition of Done confirms that the required live registers and review process exist and are usable. They may move to **Verified** only after:

- the controlled acceptance criteria are checked;
- the risk backlog is consolidated and important risks have complete controls;
- critical assumptions are reviewed against evidence;
- constraints are checked against their authoritative sources and current scope;
- relevant licences have exact-version and packaging decisions;
- the cross-register consistency audit passes or records accepted gaps;
- evidence records for `PD-05`, `EP-007` and any required `G-M05` claim are completed;
- the controlled RTM is updated and validated;
- the first baseline is frozen without hiding unresolved gaps.

## 9. Current open governance gates

The folder deliberately records these unresolved gates rather than hiding them:

1. the intended OpenVINO and TurboVec release scope must be aligned with the controlled Project Definition, requirements, RTM and ADRs;
2. the 252 candidate risks must be consolidated into a smaller operational set;
3. the assumptions must be tested and given evidence-backed outcomes;
4. constraint sources and effects must receive formal approval;
5. exact model, package, fork and asset licences must be completed before bundling;
6. evidence records and the first baseline remain pending.

## 10. Change control

A later change affecting the register structure, mandatory fields, status vocabulary, review rule, scoring method or baseline method must update:

1. the affected register or controlling index;
2. the Control and Validation Plan where the process changes;
3. the Review Log;
4. related evidence records;
5. the controlled RTM where task status changes;
6. the project change register when the controlled governance baseline changes.
