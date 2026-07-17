# PD-09 — App-Specific Evaluation Addendum

## 1. Evidence metadata

| Field | Value |
|---|---|
| Record ID | `WP:PD-09` |
| Record type | Work Package |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B. — project owner |
| Evidence date | 2026-07-14 |
| Validation date | 2026-07-14 |
| Validator | Arian B. — project owner |
| Validation method | Documentary review of the frozen evaluation addendum against the PD-09 Definition of Done and the controlled testing sources |

## 2. Statement being evidenced

> Create and freeze the app-specific evaluation addendum so that the remaining experiments map to the project research questions and frozen evidence schemas.

## 3. Definition of Done

- [x] The remaining runtime, optimisation, application and comparison evaluations have stable controlled identifiers.
- [x] Each evaluation family maps to one or more project research questions.
- [x] The existing 224 route-workbook test IDs remain preserved and are not silently renamed.
- [x] The later app-integrated evaluations have separate `APP-EVAL-*` identifiers.
- [x] The project-specific prompt set and quality rubric are frozen and versioned.
- [x] Required measures and units are identified.
- [x] Required evidence schemas are identified.
- [x] Entry, continuation, exit and stopping rules are defined.
- [x] Claim boundaries distinguish a completed evaluation plan from later test execution results.
- [x] Future changes are subject to versioned change control.

## 4. Evidence summary

PD-09 was completed by creating the controlled `App-Specific-Evaluation-Addendum-v1.md`. The addendum brings the previously distributed evaluation controls into one reviewable baseline.

It preserves the 224 runtime and codec IDs used by the six controlled workbooks, and adds a separate set of `APP-EVAL-01` to `APP-EVAL-12` identifiers for the later application evaluation. It also retains `EXP-TQ-COMP-001` and `EXP-TV-COMP-001` for the matched TurboQuant and TurboVec comparisons.

The addendum maps the evaluation work to `RQ1`, `RQ2`, `RQ3`, `RQ4` and the exploratory TurboVec question. It freezes the relevant prompts, rubric, evidence schemas, measures, gates, stopping rules and claim boundaries. This satisfies the PD-09 deliverable without claiming that the later experiments have already passed.

## 5. Authoritative evidence

| Evidence item | Repository path or external controlled location | What it proves | Status |
|---|---|---|---|
| App-Specific Evaluation Addendum | [App-Specific-Evaluation-Addendum-v1.md](../../../testing/App-Specific-Evaluation-Addendum-v1.md) | The remaining evaluations, IDs, RQ mappings, schemas, measures, gates, stopping rules and boundaries are frozen in one controlled baseline. | Available |
| Project Definition and research questions | [Project-Definition-v1.md](../../../planning/Project-Definition-v1.md) | Defines the project aim, `RQ1`–`RQ4`, exploratory TurboVec question and objectives used by the addendum. | Available |
| Application and Experimental Testing Strategy | [Test-Strategy.md](../../../testing/Test-Strategy.md) | Defines traceability, repeatability, raw-evidence, comparison and claim rules. | Available |
| Master Test Plan | [Master-Test-Plan.md](../../../testing/Master-Test-Plan.md) | Defines route order, entry/exit gates, comparison controls and stop conditions. | Available |
| Controlled test catalogue | [Test-ID-Catalogue-v1.1.md](../../../testing/Test-ID-Catalogue-v1.1.md) | Preserves the 224 unique runtime and codec test IDs. | Available |
| Controlled execution checklist | [Execution-Checklist.md](../../../testing/Execution-Checklist.md) | Defines the evidence actions required before, during and after each run. | Available |
| Frozen prompt set | [fixed-feasibility-prompt-set-v1.json](../../../../experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json) | Freezes `GTQ-PROMPTS-v1`, deterministic defaults and P1–P6 checks. | Available |
| Frozen quality rubric | [quality-rubric-v1.json](../../../../experiments/granite_turboquant_intel/rubrics/quality-rubric-v1.json) | Freezes `GTQ-QUALITY-RUBRIC-v1`, weighted dimensions, caps, anchors and adjudication procedure. | Available |
| Controlled testing workspace index | [docs/testing/README.md](../../../testing/README.md) | Lists the required machine-readable registers and evidence flow. | Available |
| Related engineering-practice evidence | [EP-019 evidence](../../engineering-practices/EP-019/README.md) | Records how the evaluation-freezing practice was applied and validated. | Available |
| Common evidence standard | [Evidence Record Template](../../templates/Evidence-Record-Template.md) | Confirms this record follows the repository evidence template. | Available |
| Merged testing-control changes | Pull requests `#4`, `#5` and `#8` | Preserve the controlled testing baseline, OpenVINO codec expansion and workbook change control in Git history. | Available |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass | `docs/testing/App-Specific-Evaluation-Addendum-v1.md` exists in version control. |
| Remaining evaluation families have stable IDs | Pass | The addendum preserves 224 route IDs and defines `APP-EVAL-01`–`APP-EVAL-12`, `EXP-TQ-COMP-001` and `EXP-TV-COMP-001`. |
| Evaluations map to the research questions | Pass | The addendum matrix maps each family to `RQ1`–`RQ4` or the exploratory TurboVec question. |
| Prompt set is frozen | Pass | `GTQ-PROMPTS-v1`, version 1.0, status `frozen-for-controlled-retest`. |
| Quality rubric is frozen | Pass | `GTQ-QUALITY-RUBRIC-v1`, version 1.0, status `controlling`. |
| Evidence schemas are frozen | Pass | The addendum names the traceability, run, environment, repository, build, model, configuration, device, performance, quality, failure, evidence, workbook and comparison records. |
| Gates and stopping rules are defined | Pass | The addendum and Master Test Plan define entry, continuation, exit and stop conditions. |
| Definition of Done checked | Pass | Every PD-09 criterion above was checked against an authoritative source. |
| Evidence is version-controlled or independently backed up | Pass | The controlling documents and histories are in Git; large raw evidence remains subject to the project backup policy. |
| No unresolved contradiction affects the claim | Pass with follow-up | The generated RTM snapshot still reflects the earlier `In Progress` state and must be regenerated only after the controlled RTM workbook is updated; it is not edited manually. |

**Validation result:** Validated

**Validation conclusion:**  
PD-09 is verified because the remaining evaluation work is now represented by stable identifiers, mapped to the research questions and governed by frozen prompts, rubric, evidence schemas, measures, gates, stopping rules and claim boundaries. The validation proves completion of the evaluation-planning addendum only; it does not prove completion or success of the later experiments.

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | Research and evidence requirements associated with `RQ1`, `RQ2`, `RQ3`, `RQ4`, including matched comparison and reproducibility requirements |
| Work package(s) | `PD-09`; later execution under `IM-02`–`IM-07`, `DL-01`–`DL-03`, `HE-01`–`HE-08`, `RT-01`–`RT-05`, `QX-03`–`QX-04`, `OV-02`–`OV-03`, `TV-02`–`TV-04`, `FR-01`–`FR-05` |
| Engineering practice(s) | `EP-019`, `EP-028`, `EP-029`, `EP-031`, `EP-035`, `EP-038`, `EP-040` |
| Objective(s) | Project objectives requiring application evaluation, compatibility evidence, optimisation comparison, memory assessment and final reporting |
| Research question(s) | `RQ1`, `RQ2`, `RQ3`, `RQ4`, exploratory TurboVec question |
| Test / experiment / evidence IDs | 224 controlled route IDs; `APP-EVAL-01`–`APP-EVAL-12`; `EXP-TQ-COMP-001`; `EXP-TV-COMP-001` |

## 8. Limitations, gaps and follow-up

- The addendum is a frozen evaluation plan, not a record of successful execution.
- Hardware, model, runtime, TurboQuant, QJL, PolarQuant, TurboVec, application, UX and release results remain incomplete until their matching evidence is captured and validated.
- The controlled RTM workbook must change PD-09 to `Implemented`, set validation to `Validated`, point its evidence path to this folder, and regenerate the repository snapshot after this change is reviewed and merged.
- Any later scope change, new test family or changed metric requires a controlled addendum revision rather than silently editing an old result.

## 9. Change control

Any later change that affects this evidence claim must update:

1. `docs/testing/App-Specific-Evaluation-Addendum-v1.md` or a versioned successor;
2. the affected test strategy, catalogue, workbook, register schema, prompt or rubric;
3. this `PD-09` evidence record;
4. the related `EP-019` evidence record;
5. the controlled RTM and regenerated traceability snapshot;
6. `docs/planning/Change-Log.md` where the controlled baseline changes;
7. the affected report section and execution evidence.
