# MoSCoW Requirements Baseline

> Latest TurboVec result: **DEMONSTRATOR_ONLY**, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, run `EXP-TV-COMP-001-20260902T231605Z-005`. Priorities and deferrals are unchanged.

> TurboVec checkpoint: **BLOCKED**, commit `ccab9f325e6ce2a270a87daf01ae4e443bcf2d49`, run `EXP-TV-COMP-001-20260902T225731Z-001`. Priorities and deferrals are unchanged.

**Document ID:** REQ-MOSCOW-001  
**Version:** 1.2  
**Presentation revision:** Categorised requirement views (CHG-014)  
**Status:** Developer-approved working baseline — supervisor review pending  
**Prepared and approved by:** Arian B  
**Effective date:** 14 July 2026  
**Supersedes:** MoSCoW Requirements Baseline v1.1  
**Related scope change:** CHG-013 / CR-013  
**Related presentation change:** CHG-014 / CR-014  

> The controlled RTM workbook is the authoritative editable source. Requirements are separated by type so Functional, Non-Functional, Research, Governance and Exclusion records are not mixed into one table.

## Baseline counts

- Requirement lifecycle records: **111**
- Active requirements: **72**
- Active Must Haves: **56**
- Active Should Haves: **14**
- Active Could Haves: **2**
- Deferred: **11**
- Superseded: **10**
- Excluded / Won't Have: **18**
- Duplicate requirement IDs: **0**
- Active Must Haves missing required traceability fields: **0**

## Requirement catalogues

- [Functional requirements](catalogue/Functional-Requirements.md)
- [Non-Functional requirements](catalogue/Non-Functional-Requirements.md)
- [Research requirements](catalogue/Research-Requirements.md)
- [Governance and project-control requirements](catalogue/Governance-Requirements.md)
- [Won't Have / exclusions and boundaries](catalogue/Exclusions-and-Boundaries.md)

Each category file lists the stable ID, current priority, release role, lifecycle and requirement statement. The linked [Requirements Traceability Matrix v1.3](Requirements-Traceability-Matrix-v1.3.md) contains the full rationale, source, objective/RQ mapping, work package, planned component, acceptance criteria, verification method, evidence ID and evidence path.

## Completion rule

A written requirement is not implementation evidence. A requirement becomes Verified only when its deliverable exists, its acceptance criteria have been checked, its evidence exists and the controlled RTM marks Validation as `Validated`.

## CHG-013 scope decisions

- F-M16, F-M17, F-M20, F-M23 and F-M24 are active Should Haves.
- F-M18 and the narrowed F-M19 remain Must Haves.
- F-M21, F-M22 and N-M11 remain bounded Must Haves for one pinned TurboQuant route, truthful activation reporting and dependable upstream fallback.
- R-M02 remains Must as the TurboVec identification and release-decision gate.
- F-M25, F-M26, F-M27 and R-M13 are Deferred from the first release.

## CHG-014 presentation decision

The catalogue was split by requirement type to improve readability. CHG-014 does not change any requirement ID, priority, release role, lifecycle state, acceptance criterion, verification method or traceability relationship.

## Approval record

| Role | Name / state | Date |
|---|---|---|
| Developer and decision owner | Arian B | 14 July 2026 |
| Supervisor review | Pending — no approval claimed | — |

Any material requirement change requires a new change record and aligned updates to the RTM, tests, evidence and report.
