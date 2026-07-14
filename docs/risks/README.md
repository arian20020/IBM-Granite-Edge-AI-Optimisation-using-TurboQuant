# Risk, Assumption, Constraint and Licence Control

**Document ID:** IDX-RACL-001  
**Version:** 0.1  
**Status:** Draft structure — content review pending  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Next review:** Pending content-population review  
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

The registers together support `G-M05`, `PD-05` and `EP-007`. Creating the files does not by itself complete or validate those tasks.

## 2. Authoritative registers

| Record type | ID prefix | Authoritative file | Main outcome |
|---|---|---|---|
| Risk | `R-` | [Risk Register](Risk-Register.md) | Open, Monitoring, Triggered, Accepted or Closed |
| Assumption | `A-` | [Assumption Register](Assumption-Register.md) | Pending, Confirmed, Rejected or Superseded |
| Constraint | `C-` | [Constraint Register](Constraint-Register.md) | Active, Changed, Removed or Superseded |
| Licence | `L-` | [Licence Register](Licence-Register.md) | Pending, Approved, Restricted, Rejected or Superseded |

Supporting control files:

- [Review Log](Review-Log.md) — records formal reviews, decisions and open actions;
- [Baseline directory](baselines/README.md) — stores reviewed frozen snapshots when a baseline is approved.

## 3. Directory structure

```text
docs/risks/
├── README.md
├── Risk-Register.md
├── Assumption-Register.md
├── Constraint-Register.md
├── Licence-Register.md
├── Review-Log.md
└── baselines/
    └── README.md
```

## 4. Common control rules

1. Use stable IDs and do not reuse an ID for a different item.
2. Keep one authoritative record and cross-reference it instead of duplicating the same entry in several files.
3. Preserve rejected assumptions, closed risks, removed constraints and rejected licences so the decision history remains visible.
4. Link each item to the requirement, work package, experiment, ADR, test, issue, pull request or external controlled evidence that supports it.
5. Record an owner, current status, last-review date and next-review date.
6. Do not claim that an assumption is confirmed or a licence is approved without reviewable evidence.
7. Do not delete negative findings, blockers or restrictions.
8. Do not commit API keys, personal data, restricted model weights, unlicensed material or confidential third-party content.
9. Use relative repository links where possible.
10. Record material changes through project change control.

## 5. Review cycle

The live registers are reviewed:

- weekly during active development;
- before a major implementation or experiment gate that depends on an entry;
- when new contradictory evidence is discovered;
- when a third-party version, model, runtime, dataset or licence changes;
- before freezing a release or evidence baseline.

Each formal review must be added to [Review-Log.md](Review-Log.md).

## 6. Baseline and freeze rule

A baseline is a reviewed snapshot of the four live registers at a defined point in time. Freezing a baseline does not mean the live registers can never change.

The process is:

```text
Draft or update live registers
        ↓
Review entries and evidence
        ↓
Record the review outcome
        ↓
Freeze an approved baseline snapshot
        ↓
Continue maintaining the live registers
        ↓
Freeze a later version when required
```

No `v1.0` baseline is claimed until the four registers have been populated, reviewed and approved for the developer working baseline.

## 7. Completion and validation boundary

The directory structure is **prepared**, not yet complete evidence for `G-M05`, `PD-05` or `EP-007`.

The tasks may move to **Implemented** only when the required records have been populated and the live register is operational. They may move to **Verified** only after:

- the controlled acceptance criteria are checked;
- assumptions are reviewed against evidence;
- high risks contain the required controls;
- constraints are consolidated;
- relevant licences are reviewed;
- evidence records for `PD-05` and `EP-007` are completed;
- the controlled RTM is updated and validated.

## 8. Change control

A later change affecting the register structure, mandatory fields, status vocabulary, review rule or baseline method must update:

1. the affected register or controlling index;
2. the Review Log;
3. the related evidence records;
4. the controlled RTM where task status changes;
5. the project change register when the controlled governance baseline changes.
