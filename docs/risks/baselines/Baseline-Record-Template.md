# [Baseline ID] — Risk, Assumption, Constraint and Licence Baseline

## 1. Baseline metadata

| Field | Value |
|---|---|
| Baseline ID | `BL-RACL-XXX` |
| Version | `vX.Y` |
| Freeze date | `YYYY-MM-DD` |
| Source commit | `[full Git commit SHA]` |
| Review ID | `RV-XXX` |
| Approval scope | Developer working baseline / Supervisor reviewed / Final release |
| Approved by | `[name or role]` |
| Supersedes | None / earlier baseline ID |
| Superseded by | None / later baseline ID |

## 2. Included files

| File | Source path | Frozen copy | Source blob or file hash | Included |
|---|---|---|---|---|
| Risk Register | `../../Risk-Register.md` | `Risk-Register.md` | `[SHA or hash]` | Yes / No |
| Assumption Register | `../../Assumption-Register.md` | `Assumption-Register.md` | `[SHA or hash]` | Yes / No |
| Constraint Register | `../../Constraint-Register.md` | `Constraint-Register.md` | `[SHA or hash]` | Yes / No |
| Licence Register | `../../Licence-Register.md` | `Licence-Register.md` | `[SHA or hash]` | Yes / No |

## 3. Review conclusion

State what was reviewed, which criteria passed and the exact approval boundary.

### What this baseline proves

[Precise statement]

### What this baseline does not prove

[Precise boundary]

## 4. Open gaps and accepted residual items

| Gap / item ID | Description | Effect | Decision | Owner | Target date |
|---|---|---|---|---|---|
| `[ID]` | `[description]` | `[effect]` | Accepted / Pending action / Blocks release | `[owner]` | `YYYY-MM-DD` |

Write `None` only when the baseline review found no relevant open gap.

## 5. Approval checks

| Check | Result | Evidence / note |
|---|---|---|
| All four live registers reviewed | Pass / Fail / Pending | `[note]` |
| Critical and High risks have complete controls | Pass / Fail / Pending | `[note]` |
| Critical assumptions have outcomes or assigned pending actions | Pass / Fail / Pending | `[note]` |
| Active constraints have source, impact and response | Pass / Fail / Pending | `[note]` |
| Release-relevant licences have packaging decisions | Pass / Fail / Pending | `[note]` |
| Cross-register consistency audit passed | Pass / Fail / Pending | `[note]` |
| Review Log updated | Pass / Fail / Pending | `[review link]` |
| Evidence and RTM status are consistent | Pass / Fail / Pending | `[note]` |

**Baseline decision:** Approved / Rejected / Deferred

**Decision reason:**  
[Explain the decision.]

## 6. Change rule

This frozen baseline must not be edited to incorporate later findings. Update the live registers, record a new review and freeze a later baseline version that supersedes this one.
