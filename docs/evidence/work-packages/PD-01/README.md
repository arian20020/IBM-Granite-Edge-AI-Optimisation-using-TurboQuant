# PD-01 — Freeze First-Release Definition and Research Questions

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `WP:PD-01` |
| Record type | Work Package |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B. — project owner |
| Evidence date | 2026-07-11 |
| Validation date | 2026-07-14 |
| Validator | Arian B. — project owner |
| Validation method | Documentary review against the PD-01 Definition of Done and related requirement `G-M01` |

## 2. Statement being evidenced

> Freeze the first-release project definition and research questions.

## 3. Definition of Done

- [x] One clear project aim is version-controlled.
- [x] The research questions are defined and measurable through the planned project evidence.
- [x] The project objectives are version-controlled.
- [x] The project contribution is stated.
- [x] The first-release scope and scope tiers are stated.
- [x] Exclusions and project boundaries are stated.
- [x] A satisfactory project outcome is defined.
- [x] The controlled baseline is linked from the repository README.

## 4. Evidence summary

The project definition was frozen in `docs/planning/Project-Definition-v1.md`. It is the single authoritative baseline for the project problem, aim, research questions, objectives, contribution, first-release scope, exclusions, boundaries and satisfactory outcome.

This work-package evidence record does not copy that content. It cross-references the authoritative document so later changes remain controlled and traceable.

## 5. Authoritative evidence

| Evidence item | Repository path | What it proves | Status |
|---|---|---|---|
| Project Definition and Scope Baseline | [Project-Definition-v1.md](../../../planning/Project-Definition-v1.md) | The problem, aim, RQs, objectives, contribution, scope, exclusions and satisfactory outcome are documented in one version-controlled baseline. | Available |
| Repository README | [README.md](../../../../README.md) | The controlled Project Definition is discoverable from the repository entry point. | Available |
| Requirements Traceability Matrix | [Requirements-Traceability-Matrix.md](../../../requirements/Requirements-Traceability-Matrix.md) | The frozen definition is connected to controlled requirement, work-package and evidence IDs. | Available |
| Common evidence standard | [Evidence Record Template](../../templates/Evidence-Record-Template.md) | This record follows the shared evidence structure used across the project. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required baseline document exists | Pass | `docs/planning/Project-Definition-v1.md` is version-controlled. |
| One project aim is stated | Pass | Project Definition, section 2. |
| Research questions are defined | Pass | Project Definition, section 3. |
| Objectives are defined | Pass | Project Definition, section 4. |
| First-release scope and boundaries are defined | Pass | Project Definition, section 5. |
| Exclusions and satisfactory outcome are defined | Pass | Recorded in the Project Definition baseline. |
| Baseline is linked from repository README | Pass | Root `README.md` contains a direct relative link. |
| Evidence is not duplicated across records | Pass | Related evidence records link back to the Project Definition. |

**Validation result:** Validated

**Validation conclusion:**  
PD-01 is verified because the first-release project definition and research questions exist as a controlled repository baseline and the stated Definition of Done has been checked against that document.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement | `G-M01` |
| Work package | `PD-01` |
| Engineering practice | `EP-001` |
| Objectives | `O1`–`O11` |
| Research questions | `RQ1`, `RQ2`, `RQ3`, `RQ4`, exploratory TurboVec question |
| Authoritative source | [Project-Definition-v1.md](../../../planning/Project-Definition-v1.md) |

## 8. Limitations, gaps and follow-up

- Supervisor feedback may require a later controlled revision.
- A later revision must not silently overwrite this baseline; it must update the project change log and affected traceability records.

## 9. Change control

Any approved change to the aim, RQs, objectives, contribution, scope, exclusions or satisfactory outcome must update:

1. `docs/planning/Project-Definition-v1.md` or its successor version;
2. `docs/planning/Change-Log.md`;
3. the RTM and Task Checklist;
4. this PD-01 evidence record;
5. the related `G-M01` and `EP-001` evidence state.
