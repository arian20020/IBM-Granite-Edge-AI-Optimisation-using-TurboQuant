# Risk, Assumption, Constraint and Licence Review Log

**Document ID:** LOG-RACL-REV-001  
**Version:** 0.4  
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
| RV-002 | 2026-07-14 | Content-population and control-design review | Full `docs/risks/` working control area | Arian B | Risk records and candidates `R-001`–`R-254`; assumptions `A-001`–`A-017`; constraints `C-001`–`C-015`; licences `L-001`–`L-013`; controlling index and baseline rules | Added the project-wide candidate risk backlog; populated the core assumption, constraint and licence registers; added risk scoring, review order, licence gate, cross-register audit, Definition of Done and baseline template; updated the controlling index. | The control area became populated and operational as a draft. Candidate risks still required consolidation; assumptions, constraint review, licence gates and baseline approval remained open. | Resolve scope alignment; consolidate risks; validate assumptions; confirm constraints; complete licence decisions; create evidence records and conduct baseline review. | Arian B | Before first baseline and dependent release gates | Risk-consolidation review | PR #23; branch `docs/populate-project-risk-register` |
| RV-003 | 2026-07-14 | Internal consistency review | Register relationships, statuses, dates, control rules and packaging boundaries | Arian B | Updated registers, Control and Validation Plan, README, candidate index and baseline controls | Linked the registers to the controlled method; applied the exposure matrix to existing risks; added event-based reviews; clarified licence, evidence and baseline boundaries. | Internal document structure and terminology were consistent enough for pull-request review. The review did not validate technical assumptions, accept residual risks or authorise a baseline. | Complete the substantive evidence and gate reviews in the Control and Validation Plan. | Arian B | Before baseline review | Risk-consolidation review | PR #23; `Control-and-Validation-Plan.md` |
| RV-004 | 2026-07-14 | Risk-consolidation review | Original inventory `R-001`–`R-254` and the live operational Risk Register | Arian B | Six candidate tables, existing full risks, Assumption Register, Constraint Register, Licence Register and Failure Register boundary | Reduced the identification inventory to 37 operational risks; merged duplicate and overly narrow candidates; preserved every candidate through `Risk-Consolidation-Map.md`; completed initial ratings, triggers, validation methods, mitigation, contingency, owner, status, planned evidence and review timing for retained risks; marked candidate files as historical discovery evidence. | **Consolidation passed for the developer working register.** The live risk set is now manageable and contains the important project uncertainties without duplicate operational rows. This review approves the consolidation logic and initial treatment plan; it does not prove that planned controls have been implemented or that residual risks are accepted. | Review treatment evidence at each dependent gate; reassess residual risks; resolve the controlled scope mismatch; complete assumption, constraint and licence reviews before baseline approval. | Arian B | At each dependent gate and before baseline review | Scope-alignment and assumption-gate reviews | PR #23; `Risk-Register.md`; `Risk-Consolidation-Map.md`; commit history |

## Review types

| Review type | Purpose |
|---|---|
| Structural initialisation | Creates or changes the register format without approving substantive entries. |
| Content-population review | Confirms that the important known entries and their required fields have been captured without claiming validation. |
| Internal consistency review | Checks vocabulary, relationships, dates, claim boundaries and control rules across the folder. |
| Risk-consolidation review | Reduces an identification backlog to a manageable operational set and preserves merge/supersession history. |
| Routine review | Checks owners, status, triggers, evidence and next actions. |
| Gate review | Reviews entries before a dependent implementation, experiment, packaging or release decision. |
| Incident review | Responds to a triggered risk, rejected assumption, changed constraint or licence problem. |
| Baseline review | Determines whether the four registers are ready to be frozen as a controlled snapshot. |
| Final release review | Confirms that release evidence, residual risks, constraints and licence decisions are current. |

## Required future reviews

| Planned review | Current state | Minimum purpose | Blocking condition |
|---|---|---|---|
| Scope-alignment review | Pending | Resolve or explicitly retain the difference between intended OpenVINO/TurboVec scope and the controlled baseline | First baseline cannot silently hide the mismatch |
| Risk-consolidation review | Completed as `RV-004` | Maintain the 37-risk operational set and prevent duplicate growth | New risks need materially different treatment; Critical/High controls still require evidence at gates |
| Assumption gate review | Pending | Validate assumptions needed by the next technical route | A dependent implementation or claim cannot rely on an unexamined assumption |
| Constraint review | Pending | Confirm authoritative sources and that project plans comply with each active boundary | No baseline with an unreviewed material constraint |
| Licence and packaging review | Pending | Pin exact components and approve, restrict or reject release use | No Pending component may be bundled |
| Baseline review | Pending | Check freeze criteria and approve or reject `v1.0` | No baseline without a recorded decision and source commit |
| Final release review | Pending | Confirm the actual package, residual risks, licence notices, evidence and claims | No release where records contradict the package or RTM |

## Review rule

A baseline review must not pass while a required Critical or High risk control, critical assumption outcome, active constraint response or release-relevant licence decision remains missing without an explicit accepted gap, owner and target date.

A consolidation review proves that the important risks have been identified, merged and given proportionate treatment plans. It does not prove that a technical route works, an assumption is true, a licence permits packaging or every risk control has already been implemented.
