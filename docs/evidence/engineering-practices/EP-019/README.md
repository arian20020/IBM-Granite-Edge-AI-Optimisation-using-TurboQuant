# EP-019 — Freeze App-Specific Evaluation Addendum

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `EP:EP-019` |
| Record type | Engineering Practice |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B. — project owner |
| Evidence date | 2026-07-14 |
| Validation date | 2026-07-14 |
| Validator | Arian B. — project owner |
| Validation method | Documentary inspection of the frozen evaluation addendum, controlled testing strategy, identifiers, prompt/rubric versions, schemas and gates |

## 2. Statement being evidenced

> Freeze the app-specific evaluation addendum before later development and final evaluation so that experiment identifiers, research-question mappings, prompts, rubric, measures, evidence schemas and stopping rules are controlled.

## 3. Definition of Done

- [x] A versioned app-specific evaluation addendum exists.
- [x] Existing route-workbook identifiers remain stable and traceable.
- [x] Later app-integrated evaluation identifiers are frozen before execution.
- [x] Evaluations map to the project research questions.
- [x] The prompt set and scoring rubric are versioned and frozen.
- [x] Measures and required evidence records are defined.
- [x] Entry, continuation, exit and stopping rules are explicit.
- [x] Failed, blocked and inconclusive evidence must remain visible.
- [x] Requested and actual backend, device and optimisation state are kept separate.
- [x] The planning claim is bounded so it cannot be mistaken for a successful test result.
- [x] Later changes require controlled versioning and traceability updates.

## 4. Evidence summary

EP-019 was applied by freezing `EVAL-ADDENDUM-v1` and linking it to the existing controlled testing workspace.

The practice preserves the 224 route-workbook IDs, introduces separate `APP-EVAL-01` to `APP-EVAL-12` identifiers for later application evaluation, and retains the matched comparison IDs `EXP-TQ-COMP-001` and `EXP-TV-COMP-001`. Each evaluation family is linked to the relevant research question and later work packages.

The same baseline fixes the prompt set `GTQ-PROMPTS-v1`, the quality rubric `GTQ-QUALITY-RUBRIC-v1`, the required machine-readable schemas, the key measures, and the rules governing test entry, continuation, stopping, completion and claims. The practice is therefore implemented and validated as a planning and control activity.

## 5. Authoritative evidence

| Evidence item | Repository path or external controlled location | What it proves | Status |
|---|---|---|---|
| Frozen evaluation addendum | [App-Specific-Evaluation-Addendum-v1.md](../../../testing/App-Specific-Evaluation-Addendum-v1.md) | The project-specific evaluation IDs, RQ mappings, inputs, schemas, measures, gates, stopping rules and boundaries are frozen. | Available |
| PD-09 evidence record | [PD-09 evidence](../../work-packages/PD-09/README.md) | The related work-package Definition of Done was checked and validated. | Available |
| Project Definition | [Project-Definition-v1.md](../../../planning/Project-Definition-v1.md) | Supplies the controlling aim, RQs, objectives and scope. | Available |
| Test strategy | [Test-Strategy.md](../../../testing/Test-Strategy.md) | Requires traceability, matched baselines, exact version capture, failure retention and bounded claims. | Available |
| Master Test Plan | [Master-Test-Plan.md](../../../testing/Master-Test-Plan.md) | Supplies controlled route order, gates, comparison controls and stopping rules. | Available |
| Controlled test catalogue | [Test-ID-Catalogue-v1.1.md](../../../testing/Test-ID-Catalogue-v1.1.md) | Proves that the existing 224 route and codec IDs are frozen and cannot be reused. | Available |
| Execution checklist | [Execution-Checklist.md](../../../testing/Execution-Checklist.md) | Defines the evidence actions required before, during and after execution. | Available |
| Frozen prompt set | [fixed-feasibility-prompt-set-v1.json](../../../../experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json) | Fixes P1–P6, deterministic checks and generation defaults. | Available |
| Frozen quality rubric | [quality-rubric-v1.json](../../../../experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json) | Fixes the weighted 0–10 quality method, critical caps and adjudication procedure. | Available |
| Testing workspace index | [docs/testing/README.md](../../../testing/README.md) | Lists the controlled registers and the required evidence flow. | Available |
| Common evidence standard | [Evidence Record Template](../../templates/Evidence-Record-Template.md) | Confirms this record follows the common repository evidence structure. | Available |
| Merged change history | Pull requests `#4`, `#5` and `#8` | Records creation of the controlled testing workspace, codec extension and formal workbook revision control. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | `docs/testing/App-Specific-Evaluation-Addendum-v1.md` is version-controlled. |
| Stable identifiers are defined | Pass | Existing 224 route IDs are retained; separate app and matched-comparison identifiers are frozen. |
| Research-question mapping is explicit | Pass | Every evaluation row names `RQ1`, `RQ2`, `RQ3`, `RQ4` or the exploratory TurboVec question. |
| Prompts and rubric are frozen | Pass | `GTQ-PROMPTS-v1` and `GTQ-QUALITY-RUBRIC-v1` are versioned controlling inputs. |
| Evidence schemas are defined | Pass | The addendum identifies traceability, run, environment, source, build, model, configuration, device, performance, quality, failure, evidence, completion and comparison records. |
| Gates and stopping rules are defined | Pass | Entry, continuation, exit and stop conditions are recorded. |
| Negative evidence is retained | Pass | The strategy and addendum require failed, blocked, stopped and inconclusive runs to remain visible. |
| Requested versus actual state is controlled | Pass | Device and optimisation verification is a mandatory separate record. |
| Definition of Done checked | Pass | Every EP-019 criterion above was inspected against authoritative evidence. |
| Evidence is version-controlled or independently backed up | Pass | Planning controls are in Git; large raw evidence remains governed by the independent backup policy. |
| No unresolved contradiction affects the claim | Pass with follow-up | The generated RTM snapshot is intentionally not edited by hand; the controlled workbook and generated views must be updated after this evidence change is reviewed. |

**Validation result:** Validated

**Validation conclusion:**  
EP-019 is verified because the app-specific evaluation addendum is now a versioned, controlled baseline with stable identifiers, RQ mappings, frozen prompts and rubric, required schemas and measures, execution gates, stopping rules and explicit claim boundaries. This validates the engineering practice of freezing the evaluation plan; it does not validate the later experiment outcomes.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | Requirements supporting runtime compatibility, matched optimisation comparison, evidence preservation, application reliability, output quality, reproducibility and final audit |
| Work package(s) | `PD-09`; execution work packages listed in `EVAL-ADDENDUM-v1` |
| Engineering practice(s) | `EP-019`; supports `EP-028`, `EP-029`, `EP-031`, `EP-035`, `EP-038`, `EP-040` |
| Objective(s) | Objectives covering local application evaluation, compatibility, memory estimation, runtime integration, optimisation comparison and final reporting |
| Research question(s) | `RQ1`, `RQ2`, `RQ3`, `RQ4`, exploratory TurboVec question |
| Test / experiment / evidence IDs | 224 controlled route IDs; `APP-EVAL-01`–`APP-EVAL-12`; `EXP-TQ-COMP-001`; `EXP-TV-COMP-001` |

## 8. Limitations, gaps and follow-up

- This evidence proves that the evaluation plan is frozen; it does not prove that any later test passed.
- The app-integrated, UX, accessibility, offline/privacy, release and final regression evaluations still require execution and preserved evidence.
- The controlled RTM workbook must update EP-019 to `Implemented`, set validation to `Validated`, point its evidence path to this folder, and regenerate the repository traceability snapshot after review and merge.
- New evaluation needs or corrected assumptions must create a controlled revision rather than silently changing completed evidence.

## 9. Change control

Any later change that affects this evidence claim must update:

1. `docs/testing/App-Specific-Evaluation-Addendum-v1.md` or its successor;
2. the affected test strategy, catalogue, workbook, register, prompt or rubric;
3. this `EP-019` evidence record;
4. the related `PD-09` evidence record;
5. the controlled RTM and regenerated traceability snapshot;
6. `docs/planning/Change-Log.md` where the controlled baseline changes;
7. affected execution evidence and report sections.
