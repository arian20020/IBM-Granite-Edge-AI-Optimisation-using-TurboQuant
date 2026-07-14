# Risk, Assumption, Constraint and Licence Control

**Document ID:** IDX-RACL-001  
**Version:** 0.4  
**Status:** Populated working control area — formal validation and baseline pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Scope alignment, assumption, constraint and licence gate reviews  
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

The registers support `G-M05`, `PD-05` and `EP-007`. Populating and consolidating them makes the control area operational, but does not by itself prove every assumption, licence decision or risk treatment.

## 2. Authoritative registers and supporting controls

| Record type | ID prefix | Authoritative file | Current content state | Main outcome |
|---|---|---|---|---|
| Risk | `R-` | [Risk Register](Risk-Register.md) | 254-item discovery inventory consolidated into 37 operational risks; treatment evidence remains gate-dependent | Open, Monitoring, Triggered, Accepted, Closed or Superseded |
| Assumption | `A-` | [Assumption Register](Assumption-Register.md) | `A-001`–`A-017` populated; evidence review pending | Pending, Confirmed, Rejected or Superseded |
| Constraint | `C-` | [Constraint Register](Constraint-Register.md) | `C-001`–`C-015` populated; source and scope review pending | Active, Changed, Removed or Superseded |
| Licence | `L-` | [Licence Register](Licence-Register.md) | `L-001`–`L-013` populated; exact-version and packaging review pending | Pending, Approved, Restricted, Rejected or Superseded |

Supporting control files:

- [Control and Validation Plan](Control-and-Validation-Plan.md) — defines review order, scoring, gates and Definition of Done;
- [Review Log](Review-Log.md) — records formal reviews and open actions;
- [Risk Consolidation Map](Risk-Consolidation-Map.md) — maps the broad inventory to the 37 retained operational risks;
- [Candidate Risk Backlog](candidates/README.md) — preserves the original identification history;
- [Baseline directory](baselines/README.md) — stores reviewed frozen snapshots after approval;
- [Baseline Record Template](baselines/Baseline-Record-Template.md) — controls future baseline metadata and checks.

## 3. Directory structure

```text
docs/risks/
├── README.md
├── Control-and-Validation-Plan.md
├── Risk-Register.md
├── Risk-Consolidation-Map.md
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

1. Use stable IDs and never reuse an ID for a different item.
2. Keep one authoritative record and cross-reference it instead of duplicating the same entry.
3. Preserve rejected assumptions, closed risks, removed constraints, rejected licences and merged candidate history.
4. Link each item to the requirement, work package, experiment, ADR, test, issue, pull request or controlled evidence that supports it.
5. Record an owner, current status, last-review date and next-review date.
6. Do not confirm an assumption or approve a licence without reviewable evidence.
7. Do not treat a risk as controlled until its planned treatment has been checked at the relevant gate.
8. Add a new risk only when its treatment needs are materially different; add new examples to an existing risk instead of creating duplicates.
9. Do not delete negative findings, blockers or restrictions.
10. Do not commit API keys, personal data, restricted model weights, unlicensed material or confidential third-party content.
11. Keep requested settings, actual runtime behaviour, published claims, estimates and measured results separate.
12. Record material changes through project change control.

## 5. Record-type decision guide

| Statement form | Correct location |
|---|---|
| Something may happen and affect the project | Risk Register |
| Something is currently believed and needs evidence | Assumption Register |
| Something is a fixed boundary the project must obey | Constraint Register |
| Something concerns permission to use, modify or distribute external material | Licence Register |
| Something has already failed or occurred | Failure Register, issue or incident record; retain a linked risk only when recurrence or continuing impact remains |
| Something states what the system must do | Requirements and RTM |

## 6. Review cycle

The live registers are reviewed:

- weekly during active development;
- before a major implementation or experiment gate that depends on an entry;
- when contradictory evidence appears;
- when a third-party version, model, runtime, dataset or licence changes;
- before packaging or distributing a release;
- before freezing a developer, supervisor-reviewed or final baseline.

Every formal review is recorded in [Review-Log.md](Review-Log.md).

## 7. Baseline and freeze rule

A baseline is a reviewed snapshot of the four live registers at a defined point in time. Freezing a baseline does not prevent later controlled updates.

```text
Update the live registers
        ↓
Follow the Control and Validation Plan
        ↓
Record formal reviews
        ↓
Freeze an approved snapshot
        ↓
Continue maintaining the live registers
        ↓
Freeze a later version when required
```

No `v1.0` baseline is claimed until the freeze criteria in [baselines/README.md](baselines/README.md) are satisfied.

## 8. Completion and validation boundary

The control area is now **populated, risk-consolidated and operational**.

The risk-consolidation part of the task is complete for the developer working register: 254 identified items were reduced to 37 important operational risks, with merge history preserved and initial treatment fields completed.

The related tasks may move to **Implemented** only when the controlled Definition of Done confirms that the complete live control process is usable. They may move to **Verified** only after:

- Critical and High risk treatment evidence is reviewed at dependent gates;
- critical assumptions receive evidence-backed outcomes or explicit accepted pending actions;
- constraints are checked against authoritative sources and current scope;
- release-relevant licences have exact-version and packaging decisions;
- the cross-register audit passes or records accepted gaps;
- evidence records for `PD-05`, `EP-007` and any required `G-M05` claim are completed;
- the controlled RTM is updated and validated;
- the first baseline is frozen without hiding unresolved gaps.

## 9. Current open governance gates

The folder deliberately records these unresolved gates:

1. align the intended OpenVINO and TurboVec release scope with the controlled Project Definition, requirements, RTM and ADRs;
2. validate the assumptions and record evidence-backed outcomes;
3. approve constraint sources and confirm scope consistency;
4. pin exact model, package, fork and asset licences and complete packaging decisions;
5. review Critical and High risk treatment evidence at their dependent implementation/test gates;
6. complete evidence records and freeze the first approved baseline.

## 10. Change control

A later change affecting the register structure, mandatory fields, status vocabulary, review rule, scoring method, consolidation method or baseline method must update:

1. the affected register or controlling index;
2. the Control and Validation Plan;
3. the Review Log;
4. related evidence records;
5. the controlled RTM where task status changes;
6. the project change register when the controlled governance baseline changes.
