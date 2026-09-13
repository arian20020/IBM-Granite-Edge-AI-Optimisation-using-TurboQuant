# EP-007 — Consolidate Risk, Assumption, Constraint and Licence Register

> **Template version:** 1.1.1

## 1. Evidence metadata

| Field | Value |
|---|---|
| Template version | `1.1.1` |
| Evidence record version | `1.1` |
| Record ID | `EP:EP-007` |
| Record type | Engineering Practice |
| Source baseline / version | Engineering Practice catalogue / RTM v1.3.1 |
| Working status | Implemented |
| Validation state | Validated |
| Effective status | Verified |
| Owner | Arian B |
| Evidence date | 2026-07-14 |
| Last reviewed | 2026-07-15 |
| Validation date | 2026-07-15 |
| Validator | Arian B |
| Validation independence | Self-review |
| Approval scope | Developer working baseline |
| Validation method | Review of consolidation, ownership/status controls, evidence links, formal reviews, cross-register consistency and RTM status |
| Supersedes | Evidence record v1.0 |
| Superseded by | None |

## 2. Statement being evidenced

> **EP-007 — Consolidate risk/assumption/constraint/licence register.**

## 3. Acceptance mapping

| Criterion | Result | Evidence | Conclusion |
|---|---|---|---|
| Separate authoritative register types exist | Pass | Four live registers | The project no longer relies on one mixed table. |
| Duplicate and narrow risks are consolidated | Pass | Risk Register; Consolidation Map | 254 identified items reduced to 37 operational risks. |
| Stable IDs and decision history are preserved | Pass | Consolidation Map; Git history | Merged history remains traceable. |
| Owners, statuses, evidence needs and review timing exist | Pass | Four live registers | Records are operational rather than descriptive only. |
| Planning approval is separated from technical/release proof | Pass | Assumption and Licence Registers | Pending/Restricted outcomes remain visible. |
| Reviews and maintenance rules are recorded | Pass | Control Plan; Review Log | `RV-001`–`RV-008` preserve the process. |
| Cross-register audit passes | Pass | `AUD-RACL-001` | No material contradiction prevents use. |
| Requirement and work-package evidence are linked | Pass | G-M05 and PD-05 evidence records | Traceability is bidirectional. |
| Controlled RTM status is synchronised | Pass | `ART-RTM-XLSX-001` | RTM v1.3.1 records EP-007 as Verified. |

## 4. Evidence summary and claim boundary

EP-007 was implemented through controlled structure creation, population, risk consolidation, scope correction, assumption/constraint approval, licence research and a cross-register audit. The practice now operates as a continuing governance process.

**What this proves:** EP-007 has been implemented, validated and verified for the developer working baseline.

**What this does not prove:** every future update, technical assumption, mitigation or release-licence decision is already complete.

## 5. Authoritative evidence

| ID | Evidence | Identifier | Status |
|---|---|---|---|
| EV-01 | [Risk Register](../../../risks/Risk-Register.md) and [Risk Consolidation Map](../../../risks/Risk-Consolidation-Map.md) | v0.6 / current | Available |
| EV-02 | [Assumption Register](../../../risks/Assumption-Register.md) | v0.5 | Available |
| EV-03 | [Constraint Register](../../../risks/Constraint-Register.md) | v0.3 | Available |
| EV-04 | [Licence Register](../../../risks/Licence-Register.md) and [Licence Review Notes](../../../risks/Licence-Review-Notes.md) | v0.4 / v1.1 | Available |
| EV-05 | [RACL index](../../../risks/README.md), [Control Plan](../../../risks/Control-and-Validation-Plan.md) and [Review Log](../../../risks/Review-Log.md) | Current | Available |
| EV-06 | [Cross-Register Validation Audit](../../../risks/Cross-Register-Validation-Audit.md) | `AUD-RACL-001` / `RV-008` | Available |
| EV-07 | [Controlled RTM workbook artifact](../../../requirements/RTM-Workbook-Artifact-Record.md) | v1.3.1; SHA-256 `2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96` | Available |

## 6. Validation record

| Check | Result |
|---|---|
| Practice deliverable exists | Pass |
| Acceptance criteria map to evidence | Pass |
| Claim boundary is explicit | Pass |
| Review scope and independence are explicit | Pass |
| RTM Working status matches | Pass |
| RTM Validation state matches | Pass |
| RTM Effective status matches | Pass |
| Material contradictions | None found |

**Validation conclusion:** `Validated`  
**Effective conclusion:** `Verified`

## 7. Traceability

- Requirement: `G-M05`
- Work package: `PD-05`
- Engineering practice: `EP-007`
- Audit/review: `AUD-RACL-001`; `RV-008`
- Change control: `CR-018`; `CHG-018`; `CR-019`; `CHG-019`
- Pull requests: `#22`–`#26`

## 8. Continuing controls and revalidation

Continue event-driven maintenance and final release-package licence review. Revalidate when the register schema, scope, operational risk set, evidence rules or controlled RTM materially changes.

## 9. Change control

Material changes must update the affected register, Review Log, Cross-Register Validation Audit, related PD-05/G-M05 evidence, RTM artifact and evidence indexes.
