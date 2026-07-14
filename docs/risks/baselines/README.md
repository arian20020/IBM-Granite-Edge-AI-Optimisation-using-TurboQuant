# Risk, Assumption, Constraint and Licence Baselines

**Document ID:** IDX-RACL-BL-001  
**Version:** 0.2  
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

Use [Baseline-Record-Template.md](Baseline-Record-Template.md) when preparing a future baseline review.

## Current baseline state

No `v1.0` baseline has been approved or frozen yet.

The live registers have been populated, but substantive work remains:

- the risk candidate backlog must be consolidated and assessed;
- assumptions require evidence-backed outcomes or assigned pending actions;
- constraint sources and release-scope consistency require review;
- release-relevant package, model and fork licences require exact-version decisions;
- the cross-register audit, evidence records and RTM validation remain incomplete.

Creating a baseline before that review would give a false impression that the content and decisions had been approved.

## Freeze criteria

A baseline may be created only when:

1. all four live registers have been reviewed;
2. required entries have stable IDs, owners, statuses and review dates;
3. Critical and High risks contain causes, triggers, validation, mitigation, contingency and residual-risk decisions;
4. critical assumptions have evidence-backed outcomes or explicit assigned pending actions;
5. active constraints have authoritative sources, impacts and approved project responses;
6. release-relevant licences have reviewable exact-version and packaging decisions;
7. the cross-register consistency audit has passed or records explicit accepted gaps;
8. the review outcome is recorded in `../Review-Log.md`;
9. unresolved gaps are explicit and assigned;
10. the baseline version, date, source commit, review ID and approval scope are recorded;
11. the evidence records and RTM do not contradict the baseline status.

## Naming convention

Use one directory for each approved snapshot:

```text
baselines/
├── README.md
├── Baseline-Record-Template.md
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
| Source commit | Full Git commit identifying the reviewed live registers |
| Review ID | Related entry from `Review-Log.md` |
| Approval scope | Developer working baseline / Supervisor reviewed / Final release |
| Approved by | Name or role |
| Included files | Exact frozen register files and hashes |
| Claim boundary | What the baseline proves and does not prove |
| Open gaps | Explicit residual gaps or `None` after review |
| Supersedes | Earlier baseline or `None` |
| Superseded by | Later baseline or `None` |

## Freeze procedure

1. Complete the stages in `../Control-and-Validation-Plan.md`.
2. Record the formal Baseline Review in `../Review-Log.md`.
3. Create the new version directory.
4. Copy the exact reviewed live registers without rewriting them.
5. Complete `Baseline-Record.md`, including the source commit and open gaps.
6. Commit the snapshot and link the commit or pull request from the Review Log.
7. Update this index to identify the current approved baseline.

## Change rule

Do not edit an approved baseline snapshot to reflect later findings. Update the live registers, record a new review and freeze a later baseline version.
