# Risk, Assumption, Constraint and Licence Review Log

**Document ID:** LOG-RACL-REV-001  
**Version:** 0.2  
**Status:** Active control record  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`

## Purpose

This log records formal reviews of the Risk, Assumption, Constraint and Licence registers. It preserves when the registers were reviewed, who reviewed them, what changed, what decisions were made and what actions remain.

Routine text edits do not require a separate review entry unless they alter an item’s meaning, evidence, status, owner, treatment or decision.

## Review records

| Review ID | Review date | Review type | Scope | Reviewer(s) | Records reviewed | Changes made | Review outcome | Open actions | Action owner | Target date | Next review | Evidence / PR |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| RV-001 | 2026-07-14 | Structural initialisation | Directory structure and register schemas | Arian B | Existing mixed register containing `R-001`, `R-002` and `A-001` | Created four dedicated registers, retained the two existing risks, moved `A-001` into the Assumption Register and created baseline controls. | Structure prepared; register content, validation and first baseline remained pending. No `PD-05`, `EP-007` or `G-M05` completion claim was made. | Populate candidate risks, assumptions, constraints and licences; review evidence; freeze first approved baseline; complete evidence records; update RTM only after validation. | Arian B | Pending | Content-population review | `CR-018`; `CHG-018`; initialisation pull request |
| RV-002 | 2026-07-14 | Content-population and control-design review | Full `docs/risks/` working control area | Arian B | Risk records and candidates `R-001`–`R-254`; assumptions `A-001`–`A-017`; constraints `C-001`–`C-015`; licences `L-001`–`L-013`; controlling index and baseline rules | Added the project-wide candidate risk backlog; populated the core assumption, constraint and licence registers; added risk scoring, review order, licence gate, cross-register audit, Definition of Done and baseline template; updated the controlling index. | The control area is populated and operational as a draft. Candidate risks are not yet consolidated or assessed; assumptions remain Pending; constraint source/scope review remains; several licence decisions remain Pending or Restricted; no `v1.0` baseline or Verified task claim is approved. | Resolve the intended-scope/baseline mismatch; consolidate and assess risks; execute assumption validations; confirm constraint sources; pin exact model/runtime/package versions; complete licence packaging decisions; run cross-register audit; create evidence records; conduct baseline review. | Arian B | Before first baseline and dependent release gates | Formal risk, assumption, constraint and licence gate review | PR #23; commit chain on `docs/populate-project-risk-register` |

## Review types

| Review type | Purpose |
|---|---|
| Structural initialisation | Creates or changes the register format without approving substantive entries. |
| Content-population review | Confirms that the important known entries and their required fields have been captured without claiming validation. |
| Routine review | Checks owners, status, triggers, evidence and next actions. |
| Gate review | Reviews entries before a dependent implementation, experiment, packaging or release decision. |
| Incident review | Responds to a triggered risk, rejected assumption, changed constraint or licence problem. |
| Baseline review | Determines whether the four registers are ready to be frozen as a controlled snapshot. |
| Final release review | Confirms that release evidence, residual risks, constraints and licence decisions are current. |

## Required future reviews

| Planned review | Minimum purpose | Blocking condition |
|---|---|---|
| Scope-alignment review | Resolve or explicitly retain the difference between intended OpenVINO/TurboVec scope and the current controlled baseline | First baseline cannot silently hide the mismatch |
| Risk-consolidation review | Reduce the candidate backlog to an operational risk set and complete controls for Critical and High risks | No baseline while important risk treatment fields are blank |
| Assumption gate review | Validate assumptions needed by the next technical route | A dependent implementation or claim cannot rely on an unexamined assumption |
| Licence and packaging review | Pin exact components and approve, restrict or reject release use | No Pending component may be bundled |
| Baseline review | Check the freeze criteria and approve or reject `v1.0` | No baseline without a recorded decision and source commit |
| Final release review | Confirm the actual release package, residual risks, licence notices, evidence and report claims | No release or final claim where records contradict the package or RTM |

## Review rule

A baseline review must not pass while a required Critical or High risk control, critical assumption outcome, active constraint response or release-relevant licence decision remains missing without an explicit accepted gap, owner and target date.

A content-population review proves that the records and process exist. It does not prove that a technical route works, an assumption is true, a licence permits every packaging choice or the related RTM tasks are Verified.
