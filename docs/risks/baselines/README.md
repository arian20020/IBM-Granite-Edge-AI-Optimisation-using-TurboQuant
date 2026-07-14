# Risk, Assumption, Constraint and Licence Baselines

**Document ID:** IDX-RACL-BL-001  
**Version:** 0.1  
**Status:** Prepared — no approved baseline yet  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`

## Purpose

This directory stores frozen, reviewed snapshots of the four live registers in the parent directory.

The live authoritative files remain:

- `../Risk-Register.md`;
- `../Assumption-Register.md`;
- `../Constraint-Register.md`;
- `../Licence-Register.md`;
- `../Review-Log.md`.

## Current baseline state

No `v1.0` baseline has been approved or frozen yet.

The initial structure is intentionally labelled version `0.1` because the project still needs to discuss, populate and review the substantive records. Creating a baseline file before that review would give a false impression that the content had been approved.

## Freeze criteria

A baseline may be created only when:

1. all four live registers have been reviewed;
2. required entries have stable IDs, owners, statuses and review dates;
3. high risks contain triggers, validation, mitigation and contingency;
4. critical assumptions have evidence-backed outcomes or explicit pending actions;
5. active constraints have sources, impacts and project responses;
6. release-relevant licences have reviewable decisions and restrictions;
7. the review outcome is recorded in `../Review-Log.md`;
8. unresolved gaps are explicit and assigned;
9. the baseline version, date, commit and approval scope are recorded.

## Naming convention

Use one directory for each approved snapshot:

```text
baselines/
├── README.md
├── v1.0/
│   ├── Baseline-Record.md
│   ├── Risk-Register.md
│   ├── Assumption-Register.md
│   ├── Constraint-Register.md
│   └── Licence-Register.md
└── v1.1/
    └── ...
```

## Baseline record fields

Each future `Baseline-Record.md` must contain:

| Field | Required value |
|---|---|
| Baseline ID | Stable ID such as `BL-RACL-001` |
| Version | `v1.0`, `v1.1`, etc. |
| Freeze date | `YYYY-MM-DD` |
| Source commit | Git commit identifying the reviewed live registers |
| Review ID | Related entry from `Review-Log.md` |
| Approval scope | Developer working baseline / Supervisor reviewed / Final release |
| Approved by | Name or role |
| Included files | Exact frozen register files |
| Open gaps | Explicit residual gaps or `None` after review |
| Supersedes | Earlier baseline or `None` |
| Superseded by | Later baseline or `None` |

## Change rule

Do not edit an approved baseline snapshot to reflect later findings. Update the live registers, record a new review and freeze a later baseline version.
