# Risk, Assumption, Constraint and Licence Review Log

**Document ID:** LOG-RACL-REV-001  
**Version:** 0.5  
**Status:** Active control record  
**Owner:** Arian B  
**Effective date:** 2026-07-14  
**Last reviewed:** 2026-07-14  
**Related requirement:** `G-M05`  
**Related work package:** `PD-05`  
**Related engineering practice:** `EP-007`  
**Related change:** `CR-018` / `CHG-018`

## Purpose

This log records formal reviews of the Risk, Assumption, Constraint and Licence control area. It preserves who reviewed the records, what was decided, what changed and what still remains.

Routine wording changes do not need a separate entry unless they change an item’s meaning, evidence, status, owner, treatment or decision.

## Review records

| Review ID | Date | Review type | Scope | Reviewer | Main decision and changes | Outcome | Remaining actions | Evidence |
|---|---|---|---|---|---|---|---|---|
| RV-001 | 2026-07-14 | Structural initialisation | Register structure and schemas | Arian B | Created separate Risk, Assumption, Constraint and Licence registers; retained `R-001`, `R-002` and `A-001`; added baseline controls. | Structure prepared. No completion or validation claim made. | Populate the registers, review evidence and freeze a later approved baseline. | `CR-018`; `CHG-018`; initialisation pull request |
| RV-002 | 2026-07-14 | Content-population review | Full `docs/risks/` area | Arian B | Added the broad risk inventory and populated `A-001`–`A-017`, `C-001`–`C-015` and `L-001`–`L-013`; added scoring, review and baseline controls. | The folder became populated and operational as a draft. | Consolidate risks; validate assumptions; review constraints and licences; create evidence records. | PR #23 and its commit history |
| RV-003 | 2026-07-14 | Internal consistency review | Relationships, statuses, dates, claim boundaries and packaging rules | Arian B | Checked vocabulary and links; applied the exposure matrix; clarified that technical proof, licence permission, packaging approval and RTM validation are separate decisions. | Internal structure was consistent enough for review. | Complete substantive risk, assumption, constraint, licence and baseline reviews. | PR #23; `Control-and-Validation-Plan.md` |
| RV-004 | 2026-07-14 | Risk-consolidation review | Original inventory `R-001`–`R-254` | Arian B | Reduced 254 identified items to 37 operational risks; merged duplicates and narrow examples; preserved every original ID through `Risk-Consolidation-Map.md`; completed initial ratings and treatment plans. | **Passed for the developer working register.** Consolidation is complete, but treatment evidence still requires gate review. | Review risk controls at their dependent gates and reassess residual risks. | PR #24; `Risk-Register.md`; `Risk-Consolidation-Map.md` |
| RV-005 | 2026-07-14 | Scope-alignment review | Project Definition, MoSCoW requirements, RTM roles, ADR-TurboVec and risk records | Arian B | Confirmed the intended current roles: Windows 11 x64, Intel, Granite, llama.cpp and TurboQuant are core; OpenVINO is a Should Have; TurboVec is a later feasibility investigation and full integration remains deferred. Corrected `R-017` from a current Critical mismatch to a Medium future document-drift risk. Adjusted TurboVec risks to match their deferred timing. | **Passed for the current developer working baseline.** The present scope is aligned and no scope-change request is required. | Use change control if OpenVINO is promoted to Must Have or full TurboVec integration is reactivated. | Project Definition §4–§5; MoSCoW catalogue; ADR-TurboVec; corrected Risk Register; PR #24 |

## Review types

| Review type | Purpose |
|---|---|
| Structural initialisation | Creates or changes the register structure without approving the content. |
| Content-population review | Checks that important known entries have been captured. |
| Internal consistency review | Checks terminology, links, dates, statuses and claim boundaries. |
| Risk-consolidation review | Reduces a broad discovery list to a manageable operational set while preserving history. |
| Scope-alignment review | Confirms that planning, requirements, RTM, ADRs and work packages use the same release roles. |
| Routine review | Checks owners, statuses, triggers, evidence and next actions. |
| Gate review | Checks records before dependent implementation, experiment, packaging or release work. |
| Incident review | Responds to a triggered risk, rejected assumption, changed constraint or licence problem. |
| Baseline review | Approves or rejects a frozen snapshot. |
| Final release review | Checks the actual release package, residual risks, licence notices, evidence and claims. |

## Required future reviews

| Planned review | Current state | Minimum purpose | Blocking condition |
|---|---|---|---|
| Scope-alignment review | Completed as `RV-005` | Recheck only after a material scope or priority change | A changed release role must not remain inconsistent across controlled records |
| Risk-consolidation review | Completed as `RV-004` | Maintain the 37-risk set and prevent duplicate growth | New risks need materially different treatment; Critical and High controls still need evidence |
| Assumption gate review | Pending | Validate assumptions needed by each technical route | A dependent claim cannot rely on an unexamined assumption |
| Constraint review | Pending | Confirm sources and project compliance | No baseline with an unreviewed material constraint |
| Licence and packaging review | Pending | Approve, restrict or reject exact release use | No Pending component may be bundled |
| Cross-register audit | Pending | Confirm that all four registers and evidence records agree | No baseline with unresolved contradictions unless explicitly accepted |
| Baseline review | Pending | Approve or reject `v1.0` | No baseline without a source commit, reviewed files and recorded decision |
| Final release review | Pending | Confirm package, evidence, residual risks, notices and claims | No release where records contradict the package or RTM |

## Review rule

A baseline review must not pass while a required Critical or High risk control, critical assumption outcome, active constraint response or release-relevant licence decision is missing without an explicit accepted gap, owner and target date.

The completed consolidation and scope-alignment reviews prove that the operational risk set and current release roles are coherent. They do not prove that OpenVINO, TurboQuant or TurboVec technically works, that assumptions are true, that licences permit final packaging or that `PD-05`, `EP-007` and `G-M05` are Verified.
