# Risk, Assumption, Constraint and Licence Review Log

**Document ID:** LOG-RACL-REV-001  
**Version:** 0.2  
**Status:** Active draft control record  
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
| RV-001 | 2026-07-14 | Structural initialisation | Directory structure and register schemas | Arian B | Existing mixed register containing `R-001`, `R-002` and `A-001` | Created four dedicated registers, retained the two existing risks, moved `A-001` into the Assumption Register and created baseline controls. | Structure prepared; register content, validation and first baseline remain pending. No `PD-05`, `EP-007` or `G-M05` completion claim is made. | Discuss and populate candidate risks, assumptions, constraints and licences; review evidence; freeze first approved baseline; complete evidence records; update RTM only after validation. | Arian B | Pending | Pending content-population review | `CR-018`; `CHG-018`; PR #22 |
| RV-002 | 2026-07-14 | Candidate inventory population | Full project risk identification covering schedule, scope, Windows, model handling, llama.cpp, OpenVINO, TurboQuant, TurboVec, Intel hardware, performance, security, UX, AI quality, testing, licensing and release reporting | Arian B | `R-001` to `R-254` | Retained the two existing assessed draft risks; added `R-003` to `R-254` as Candidate entries; added the Candidate status; recorded the intended Windows/Intel, OpenVINO, TurboQuant and TurboVec scope basis; preserved the separate requirements-scope mismatch as a risk rather than silently changing the RTM. | Candidate inventory populated. The entries have not yet been merged, scored, traced, assigned complete controls or approved as the first baseline. No task-completion or validation claim is made. | Review each candidate; remove or merge duplicates; separate assumptions, constraints, issues and licence facts; score probability and impact; define triggers, mitigations and contingencies; map evidence and IDs; update the controlled requirements through a separate change request where scope differs; then perform a baseline review. | Arian B | Pending | Pending risk assessment workshop | Candidate risk inventory pull request |

## Review types

| Review type | Purpose |
|---|---|
| Structural initialisation | Creates or changes the register format without approving substantive entries. |
| Candidate inventory population | Adds a broad set of possible risks before detailed assessment and consolidation. |
| Routine review | Checks owners, status, triggers, evidence and next actions. |
| Gate review | Reviews entries before a dependent implementation, experiment or release decision. |
| Incident review | Responds to a triggered risk, rejected assumption, changed constraint or licence problem. |
| Baseline review | Determines whether the four registers are ready to be frozen as a controlled snapshot. |
| Final release review | Confirms that the release evidence, residual risks, constraints and licence decisions are current. |

## Review rule

A baseline review must not pass while a required high-risk control, critical assumption outcome, active constraint response or release-relevant licence decision remains missing without an explicit accepted gap and owner.

A Candidate entry is not yet an assessed or controlled risk. It becomes an approved active risk only after the review confirms its wording, ownership, probability, impact, exposure, trigger, treatment, evidence and traceability.
