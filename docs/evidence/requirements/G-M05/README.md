# G-M05 — Consolidated Risk, Assumption, Constraint and Licence Register

> **Template version:** 1.1.1

## 1. Evidence metadata

| Field | Value |
|---|---|
| Template version | `1.1.1` |
| Evidence record version | `1.1` |
| Record ID | `REQ:G-M05` |
| Record type | Requirement |
| Source baseline / version | MoSCoW v1.2 / RTM v1.3.1 |
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
| Validation method | Documentary requirement review, cross-register audit and controlled RTM status verification |
| Supersedes | Evidence record v1.0 |
| Superseded by | None |

## 2. Statement being evidenced

> **G-M05 — The project must maintain a consolidated risk, assumption, constraint and licence register.**

## 3. Acceptance-criterion mapping

| Criterion | Result | Evidence | Conclusion |
|---|---|---|---|
| Authoritative Risk Register exists | Pass | Risk Register; Risk Consolidation Map | 37 operational risks retain full history. |
| Authoritative Assumption Register exists | Pass | Assumption Register | `A-001`–`A-017` are controlled with evidence-gated outcomes. |
| Authoritative Constraint Register exists | Pass | Constraint Register | `C-001`–`C-015` are approved Active boundaries. |
| Authoritative Licence Register exists | Pass | Licence Register; Licence Review Notes | Use, modification, redistribution and packaging decisions are separated. |
| The records operate as one consolidated control system | Pass | RACL README; Control and Validation Plan; Review Log | Shared governance, reviews and change control exist. |
| High/Critical risks contain required treatment fields | Pass | Risk Register; `AUD-RACL-001` | Probability, impact, trigger, validation, mitigation, contingency, owner and status are present. |
| Formal validation review completed | Pass | `AUD-RACL-001`; `RV-008` | No material contradiction prevents use. |
| RTM status matches the evidence conclusion | Pass | `ART-RTM-XLSX-001` | RTM v1.3.1 records `Implemented / Validated / Verified`. |

## 4. Evidence summary and claim boundary

The project maintains separate authoritative Risk, Assumption, Constraint and Licence registers under one controlled governance area. The original broad risk inventory was consolidated from 254 identified items to 37 operational risks without losing traceability. Assumptions and constraints received formal review decisions, and the licence review records source-level permissions and release restrictions.

**What this proves:** G-M05 is implemented, validated and verified for the developer working baseline.

**What this does not prove:** every technical assumption, mitigation or final release-package licence decision has already passed. Those remain event-driven controls.

## 5. Authoritative evidence

| ID | Evidence | Version / identifier | Status |
|---|---|---|---|
| EV-01 | [Risk Register](../../../risks/Risk-Register.md) | v0.6 | Available |
| EV-02 | [Risk Consolidation Map](../../../risks/Risk-Consolidation-Map.md) | Current | Available |
| EV-03 | [Assumption Register](../../../risks/Assumption-Register.md) | v0.5 | Available |
| EV-04 | [Constraint Register](../../../risks/Constraint-Register.md) | v0.3 | Available |
| EV-05 | [Licence Register](../../../risks/Licence-Register.md) and [Licence Review Notes](../../../risks/Licence-Review-Notes.md) | v0.4 / v1.1 | Available |
| EV-06 | [Control and Validation Plan](../../../risks/Control-and-Validation-Plan.md) and [Review Log](../../../risks/Review-Log.md) | Current | Available |
| EV-07 | [Cross-Register Validation Audit](../../../risks/Cross-Register-Validation-Audit.md) | `AUD-RACL-001` / `RV-008` | Available |
| EV-08 | [Controlled RTM workbook artifact](../../../requirements/RTM-Workbook-Artifact-Record.md) | v1.3.1; SHA-256 `2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96` | Available |

## 6. Validation record

| Check | Result |
|---|---|
| Deliverable exists | Pass |
| Criteria map to evidence | Pass |
| Claim boundary is proportionate | Pass |
| Validation independence and approval scope are explicit | Pass |
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

Revalidate when the register schema, scope, risk set, assumptions, constraints, licence decisions, RTM rules or contradictory evidence materially changes. Continue final release-package licensing and risk-treatment reviews at their stated gates; these do not reopen G-M05 unless they invalidate the governance system itself.

## 9. Change control

Material changes must update the affected register, audit/review record, related PD-05 and EP-007 evidence, the controlled RTM artifact and relevant indexes.
