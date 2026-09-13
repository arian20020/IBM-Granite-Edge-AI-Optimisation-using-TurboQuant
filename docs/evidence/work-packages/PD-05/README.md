# PD-05 — Risk, Assumption, Constraint and Licence Register

> **Template version:** 1.1.1

## 1. Evidence metadata

| Field | Value |
|---|---|
| Template version | `1.1.1` |
| Evidence record version | `1.1` |
| Record ID | `WP:PD-05` |
| Record type | Work Package |
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
| Validation method | Documentary audit of the four registers, risk consolidation, review history, licence evidence and controlled RTM status |
| Supersedes | Evidence record v1.0 |
| Superseded by | None |

## 2. Statement being evidenced

> **PD-05 — Risk/assumption/constraint/licence register.**

Definition of Done:

> High risks have validation, mitigation, contingency, owner and status.

## 3. Definition-of-Done mapping

| Criterion | Result | Evidence | Conclusion |
|---|---|---|---|
| Four authoritative register types exist | Pass | Risk, Assumption, Constraint and Licence Registers | The governance area is operational. |
| Risk discovery list is consolidated | Pass | Risk Register; Consolidation Map | 254 identified items reduced to 37 operational risks. |
| High/Critical risks have required treatment fields | Pass | Risk Register; `AUD-RACL-001` | Validation, mitigation, contingency, owner and status are present, together with the wider operational schema. |
| Assumptions have owners and validation gates | Pass | Assumption Register | Planning approval is separated from evidence confirmation. |
| Constraints are approved and controlled | Pass | Constraint Register | `C-001`–`C-015` are Active. |
| Licence permissions and restrictions are recorded | Pass | Licence Register; Licence Review Notes | Final bundling is not confused with source use. |
| Formal reviews and maintenance rules exist | Pass | Review Log; Control and Validation Plan | `RV-001`–`RV-008` preserve decisions. |
| Cross-register audit passes | Pass | `AUD-RACL-001` | No material contradiction prevents use. |
| RTM mirrors the validated conclusion | Pass | `ART-RTM-XLSX-001` | RTM v1.3.1 records PD-05 as Verified. |

## 4. Evidence summary and claim boundary

PD-05 produced a live and proportionate RACL control system. The retained operational risks contain the required treatment fields, while assumptions, constraints and licence decisions are separately controlled and honestly preserve Pending or Restricted outcomes.

**What this proves:** PD-05 is implemented, validated and verified for the developer working baseline.

**What this does not prove:** every risk treatment has already succeeded, every assumption is confirmed or the final software package is approved for distribution.

## 5. Authoritative evidence

| ID | Evidence | Identifier | Status |
|---|---|---|---|
| EV-01 | [Risk Register](../../../risks/Risk-Register.md) | v0.6 | Available |
| EV-02 | [Risk Consolidation Map](../../../risks/Risk-Consolidation-Map.md) | Current | Available |
| EV-03 | [Assumption Register](../../../risks/Assumption-Register.md) | v0.5 | Available |
| EV-04 | [Constraint Register](../../../risks/Constraint-Register.md) | v0.3 | Available |
| EV-05 | [Licence Register](../../../risks/Licence-Register.md) and [Licence Review Notes](../../../risks/Licence-Review-Notes.md) | v0.4 / v1.1 | Available |
| EV-06 | [Control and Validation Plan](../../../risks/Control-and-Validation-Plan.md); [Review Log](../../../risks/Review-Log.md) | Current | Available |
| EV-07 | [Cross-Register Validation Audit](../../../risks/Cross-Register-Validation-Audit.md) | `AUD-RACL-001` / `RV-008` | Available |
| EV-08 | [Controlled RTM workbook artifact](../../../requirements/RTM-Workbook-Artifact-Record.md) | v1.3.1; SHA-256 `2414c6790c2815a75edfcab0c9f35462dd334fe14d8c16e9810e80261f098e96` | Available |

## 6. Validation record

| Check | Result |
|---|---|
| Required deliverable exists | Pass |
| Definition of Done checked | Pass |
| Criteria link to authoritative evidence | Pass |
| Claim boundary is proportionate | Pass |
| Review scope and independence are explicit | Pass |
| RTM status agrees | Pass |
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

Risk treatments, assumptions, constraints and licences continue to be reviewed at their dependent gates. Revalidate PD-05 only when later evidence shows that the register system or its Definition of Done is no longer satisfied.

## 9. Change control

Material changes must update the affected registers, validation audit, related G-M05 and EP-007 evidence, RTM artifact and evidence indexes.
