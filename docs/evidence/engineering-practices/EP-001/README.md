# EP-001 — Freeze Problem, Aim, Research Questions, Contribution and Scope

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `EP:EP-001` |
| Record type | Engineering Practice |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B. — project owner |
| Evidence date | 2026-07-11 |
| Validation date | 2026-07-14 |
| Validator | Arian B. — project owner |
| Validation method | Documentary inspection of the controlled Project Definition and PD-01 evidence record |

## 2. Statement being evidenced

> Freeze the project problem, aim, research questions, contribution and first-release scope before implementation expands.

## 3. Definition of Done

- [x] The problem statement is version-controlled.
- [x] The project aim is version-controlled.
- [x] The research questions are version-controlled.
- [x] The objectives and intended contribution are version-controlled.
- [x] The first-release scope and exclusions are explicit.
- [x] The satisfactory outcome is stated.
- [x] Related requirement and work-package records point to the same authoritative source.
- [x] Future changes are subject to change control.

## 4. Evidence summary

EP-001 was applied by creating and freezing the Project Definition and Scope Baseline before the main implementation work. The baseline establishes what problem the project addresses, what it aims to produce and evaluate, which research questions it will answer, what is included in the first release, and what is outside the project boundary.

The same authoritative Project Definition supports `G-M01`, `PD-01` and `EP-001`. This avoids conflicting copies and provides one controlled source for later design, implementation, testing and report decisions.

## 5. Authoritative evidence

| Evidence item | Repository path | What it proves | Status |
|---|---|---|---|
| Project Definition and Scope Baseline | [Project-Definition-v1.md](../../../planning/Project-Definition-v1.md) | The problem, aim, RQs, objectives, contribution, first-release scope, exclusions and satisfactory outcome were frozen in version control. | Available |
| PD-01 evidence record | [PD-01 evidence](../../work-packages/PD-01/README.md) | The related work-package Definition of Done was checked and validated. | Available |
| Repository README | [README.md](../../../../README.md) | The controlled baseline is visible from the repository entry point. | Available |
| Common evidence standard | [Evidence Record Template](../../templates/Evidence-Record-Template.md) | This record follows the project-wide evidence structure. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Controlled baseline exists | Pass | `docs/planning/Project-Definition-v1.md` is stored in the repository. |
| Problem, aim and RQs are explicit | Pass | Project Definition sections 1–3. |
| Objectives and contribution are explicit | Pass | Project Definition and its first-release definition. |
| Scope and exclusions are explicit | Pass | Project Definition first-release scope and boundary sections. |
| Satisfactory outcome is recorded | Pass | Included in the controlled Project Definition. |
| Related records use the same authoritative source | Pass | `G-M01`, `PD-01` and `EP-001` all point to the Project Definition. |
| Change-control expectation is recorded | Pass | PD-01 and this record require updates to the change log and RTM. |

**Validation result:** Validated

**Validation conclusion:**  
EP-001 is verified because the project problem, aim, RQs, objectives, contribution, scope, exclusions and satisfactory outcome were frozen in a version-controlled baseline before further implementation, and the related evidence records consistently reference that source.

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

- The baseline may be refined after supervisor feedback, but any refinement must be controlled and traceable.
- Verification here proves that the project definition was frozen and version-controlled; it does not prove that the later implementation requirements have been completed.

## 9. Change control

A later change to the frozen definition must update:

1. the Project Definition or its successor version;
2. `docs/planning/Change-Log.md`;
3. affected requirements, work packages, objectives, RQs and report sections;
4. the `PD-01` and `EP-001` evidence records;
5. the RTM and Task Checklist status history.
