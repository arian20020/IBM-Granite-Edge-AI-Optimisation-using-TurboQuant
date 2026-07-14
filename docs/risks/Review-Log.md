# Risk, Assumption, Constraint and Licence Review Log

**Document ID:** LOG-RACL-REV-001  
**Version:** 0.6  
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
| RV-005 | 2026-07-14 | Scope-alignment review | Project Definition, MoSCoW requirements, RTM roles, ADR-TurboVec and risk records | Arian B | Confirmed the intended current roles: Windows 11 x64, Intel, Granite, llama.cpp and TurboQuant are core; OpenVINO is a Should Have; TurboVec is a later feasibility investigation and full integration remains deferred. Corrected `R-017` from a current Critical mismatch to a Medium future document-drift risk. Adjusted TurboVec risks to match their deferred timing. | **Passed for the current developer working baseline.** The present scope is aligned and no scope-change request is required. | Use change control if OpenVINO is promoted to Must Have or full TurboVec integration is reactivated. | Project Definition §4–§5; MoSCoW catalogue; ADR-TurboVec; corrected Risk Register; PR #25 |
| RV-006 | 2026-07-14 | Assumption-content and constraint approval review | `A-001`–`A-017` and `C-001`–`C-015` | Arian B | Approved the Assumption Register as the controlled planning-assumption set while retaining evidence-dependent outcomes as `Pending`. Approved all 15 constraints as the current Active boundaries. Corrected stale risk references and clarified that the no-port rule is a project design constraint for restricted environments rather than a universal NHS claim. | **Passed for register content and active constraint approval.** Assumptions may be used for planning but are not confirmed until their validation evidence passes. Constraint compliance evidence remains gate-dependent. | Execute assumption validation methods; review constraint compliance at architecture, implementation, experiment, packaging and report gates. | `Assumption-Register.md` v0.4; `Constraint-Register.md` v0.3; Project Definition; requirements; UCL guidance |
| RV-007 | 2026-07-14 | Initial licence review | Models, runtimes, forks, NuGet dependencies, data, assets, research sources and project-source licensing | Arian B | Reviewed authoritative model cards, repository licences and official package/source records; updated `L-001`–`L-015`; approved pinned llama.cpp source use; recorded Apache/MIT permissions; restricted final package decisions where exact binaries, notices or provenance remain; added pending project-source and converted-model decisions. | **Initial source-licence review passed.** The project has a sound basis for research, development and modification of the reviewed components. One complete release bundle is not yet approved. | Pin exact release artefacts; export dependency and notice inventories; complete model/asset/data provenance; decide the project’s root licence; inspect the produced release package. | `Licence-Register.md` v0.3; `Licence-Review-Notes.md`; official IBM Granite model cards; MIT/Apache licence files; NuGet package records |

## Review types

| Review type | Purpose |
|---|---|
| Structural initialisation | Creates or changes the register structure without approving the content. |
| Content-population review | Checks that important known entries have been captured. |
| Internal consistency review | Checks terminology, links, dates, statuses and claim boundaries. |
| Risk-consolidation review | Reduces a broad discovery list to a manageable operational set while preserving history. |
| Scope-alignment review | Confirms that planning, requirements, RTM, ADRs and work packages use the same release roles. |
| Assumption-content review | Approves the controlled assumption set without confusing planning approval with evidence confirmation. |
| Constraint approval review | Confirms that listed boundaries are real, sourced and active for the current scope. |
| Licence review | Reviews exact permissions, duties and packaging restrictions for external material. |
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
| Assumption-content review | Completed as part of `RV-006` | Maintain the approved set and validate each assumption at its dependent gate | No evidence-dependent assumption may be marked Confirmed without evidence |
| Constraint approval review | Completed as part of `RV-006` | Maintain the 15 approved Active boundaries and check compliance at gates | No baseline with an unaddressed material constraint breach |
| Initial licence review | Completed as `RV-007` | Maintain source-level decisions and restrictions | Pending/Restricted items must not be bundled outside their recorded conditions |
| Final licence and packaging review | Pending | Inspect exact versions, files, notices and the produced release package | No release while a bundled item remains Pending or its duties are unmet |
| Cross-register audit | Pending | Confirm that all four registers and evidence records agree | No baseline with unresolved contradictions unless explicitly accepted |
| Baseline review | Pending | Approve or reject `v1.0` | No baseline without a source commit, reviewed files and recorded decision |
| Final release review | Pending | Confirm package, evidence, residual risks, notices and claims | No release where records contradict the package or RTM |

## Review rule

A baseline review must not pass while a required Critical or High risk control, critical assumption outcome, active constraint response or release-relevant licence decision is missing without an explicit accepted gap, owner and target date.

The completed reviews prove that the operational risk set, current scope, planning-assumption set, active constraints and initial source-licence decisions are coherent. They do not prove that every technical assumption is true, that every risk control has passed, or that one final release bundle is licensed and ready to distribute.
