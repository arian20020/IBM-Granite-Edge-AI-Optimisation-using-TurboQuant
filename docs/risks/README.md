# Risk, Assumption, Constraint and Licence Control

**Document ID:** IDX-RACL-001  
**Version:** 0.5  
**Status:** Populated, risk-consolidated and scope-aligned — formal validation and baseline pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Assumption, constraint and licence gate reviews  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`  
**Related reviews:** `RV-004`; `RV-005`

## 1. Purpose

This directory is the single controlled home for the project’s risk, assumption, constraint and licence records.

The records are kept separately because they answer different questions:

- a **risk** is something uncertain that may affect the project;
- an **assumption** is something currently believed but still needing evidence;
- a **constraint** is a boundary the project must work within;
- a **licence record** states how third-party material may be used, changed, packaged or redistributed.

The folder supports `G-M05`, `PD-05` and `EP-007`. The files now exist, are populated and use one controlled process. That does not yet mean every assumption, licence decision or risk control has passed validation.

## 2. Current release-role alignment

The current project documents are aligned around these roles:

| Area | Current role |
|---|---|
| Windows 11 x64 and WinUI 3 | Core Must Have |
| Intel hardware focus | Core Must Have |
| IBM Granite | Core model family |
| Upstream llama.cpp | Core dependable route and fallback |
| TurboQuant | Core Experimental route; one exact configuration must be proved |
| OpenVINO | Active Should Have |
| TurboVec | Later feasibility investigation; full application integration remains deferred |

Review `RV-005` confirmed that no scope-change request is needed for the current wording. A later decision to promote OpenVINO or reactivate full TurboVec integration must use change control.

## 3. Authoritative files

| Record type | ID prefix | Authoritative file | Current state |
|---|---|---|---|
| Risk | `R-` | [Risk Register](Risk-Register.md) | 254 identified items consolidated into 37 operational risks |
| Assumption | `A-` | [Assumption Register](Assumption-Register.md) | `A-001`–`A-017` populated; evidence review pending |
| Constraint | `C-` | [Constraint Register](Constraint-Register.md) | `C-001`–`C-015` populated; source and compliance review pending |
| Licence | `L-` | [Licence Register](Licence-Register.md) | `L-001`–`L-013` populated; exact-version and packaging review pending |

Supporting controls:

- [Control and Validation Plan](Control-and-Validation-Plan.md) — review order, risk scoring, gates and Definition of Done;
- [Review Log](Review-Log.md) — formal reviews, decisions and open actions;
- [Risk Consolidation Map](Risk-Consolidation-Map.md) — mapping from the original inventory to the 37 retained risks;
- [Candidate Risk Backlog](candidates/README.md) — historical identification evidence;
- [Baseline controls](baselines/README.md) — rules for reviewed frozen snapshots;
- [Baseline Record Template](baselines/Baseline-Record-Template.md) — required baseline metadata and checks.

## 4. Directory structure

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

## 5. Common rules

1. Use stable IDs and never reuse an ID for a different item.
2. Keep one authoritative record and cross-reference it instead of copying it into several files.
3. Preserve merged, closed, rejected, removed and superseded history.
4. Link records to requirements, work packages, tests, experiments, ADRs, issues, pull requests or other controlled evidence.
5. Record an owner, status, last review and next review.
6. Do not confirm an assumption or approve a licence without evidence.
7. Do not treat a risk as controlled until its planned treatment is checked at the relevant gate.
8. Add a new risk only when its treatment needs are materially different from an existing risk.
9. Do not delete failures, blockers or restrictions.
10. Do not commit secrets, personal data, restricted model weights or unlicensed material.
11. Keep requested settings, actual behaviour, published claims, estimates and measured results separate.
12. Use change control for material scope, requirement or governance changes.

## 6. Where each statement belongs

| Statement | Correct location |
|---|---|
| Something may happen and affect the project | Risk Register |
| Something is believed and needs proof | Assumption Register |
| Something is a fixed boundary | Constraint Register |
| Something concerns permission to use or distribute external material | Licence Register |
| Something has already failed | Failure Register, issue or incident record |
| Something states what the system must do | Requirements and RTM |

## 7. Review cycle

Review the live records:

- weekly during active development;
- before an implementation, experiment or packaging gate that depends on them;
- when contradictory evidence appears;
- when a model, runtime, package, dataset or licence changes;
- before a release candidate is packaged;
- before a developer, supervisor-reviewed or final baseline is frozen.

Every formal review must be recorded in [Review-Log.md](Review-Log.md).

## 8. Baseline rule

A baseline is a reviewed snapshot. It does not stop later controlled updates.

```text
Update the live registers
        ↓
Follow the Control and Validation Plan
        ↓
Record the required reviews
        ↓
Freeze the approved snapshot
        ↓
Continue maintaining the live registers
```

No `v1.0` baseline is claimed until the checks in [baselines/README.md](baselines/README.md) pass.

## 9. Completion boundary

The folder is now **populated, risk-consolidated, scope-aligned and operational**.

The risk-identification and consolidation part is complete for the developer working register. Full validation still requires:

- evidence review for Critical and High risk treatments at their dependent gates;
- evidence-backed assumption outcomes or explicitly accepted pending actions;
- source and compliance review for active constraints;
- exact-version licence and packaging decisions;
- a passing cross-register consistency audit;
- completed evidence records for `PD-05`, `EP-007` and `G-M05`;
- an RTM update that matches the reviewed evidence;
- a recorded baseline review and frozen `v1.0` snapshot.

## 10. Current open governance gates

1. Validate the assumptions in dependency order.
2. Review constraint sources, wording and project compliance.
3. Pin exact model, package, fork and asset versions and complete licence decisions.
4. Check Critical and High risk treatment evidence at the relevant implementation and test gates.
5. Run the final cross-register consistency audit.
6. Complete the evidence records and controlled RTM validation.
7. Freeze the first approved baseline.

## 11. Change control

A material change to the register structure, required fields, status vocabulary, scoring, consolidation method, review process or baseline method must update:

1. the affected register or this index;
2. the Control and Validation Plan;
3. the Review Log;
4. related evidence records;
5. the controlled RTM where status changes;
6. the project change register where the governance baseline changes.
