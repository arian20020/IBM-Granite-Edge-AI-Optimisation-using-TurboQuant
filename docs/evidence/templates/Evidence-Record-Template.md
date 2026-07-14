# [ID] — [Evidence Record Title]

> **Template version:** 1.1.1  
> Use this template for new requirement, work-package, engineering-practice and experiment evidence records. Follow the migration rules in [Evidence Template Guidance](README.md) for records created with an earlier template version.

## 1. Evidence metadata

| Field | Value |
|---|---|
| Template version | `1.1.1` |
| Evidence record version | `1.0` |
| Record ID | `[REQ/WP/EP/EXP-ID]` |
| Record type | Requirement / Work Package / Engineering Practice / Experiment |
| Source baseline / version | `[requirement, RTM, plan, workflow or experiment version]` |
| Working status | Not Started / In Progress / Implemented / Partially Verified / Verified / Blocked |
| Validation state | Not Validated / Validated |
| Effective status | Not Started / In Progress / Implemented / Partially Verified / Verified / Blocked |
| Owner | `[name or role]` |
| Evidence date | `YYYY-MM-DD` |
| Last reviewed | `YYYY-MM-DD` |
| Validation date | `YYYY-MM-DD` or Pending |
| Validator | `[name or role]` |
| Validation independence | Self-review / Automated / Peer / Supervisor / External |
| Approval scope | Developer working baseline / Technical approval / Supervisor approval / Final release approval / Not applicable |
| Validation method | `[document review / automated test / manual test / experiment audit / other]` |
| Supersedes | None / `[record ID and version]` |
| Superseded by | None / `[record ID and version]` |

The three status fields must mirror the controlled RTM. Do not invent or independently calculate a different status inside the evidence record.

## 2. Statement being evidenced

> Copy the exact requirement, work-package outcome, engineering-practice statement or experiment claim here. Preserve the wording and stable ID from the controlled source.

## 3. Definition of Done or acceptance-criterion mapping

Every criterion must have a stable local identifier and must point to the evidence that supports its result. Do not set the controlled RTM Validation field to `Validated` while required criteria remain Pending or lack evidence.

| Criterion ID | Definition of Done or acceptance criterion | Result | Evidence item ID(s) | Reviewer note |
|---|---|---|---|---|
| AC-01 | `[criterion]` | Pass / Fail / Pending / Not applicable | `[EV-01]` | `[note]` |
| AC-02 | `[criterion]` | Pass / Fail / Pending / Not applicable | `[EV-02]` | `[note]` |
| AC-03 | `[criterion]` | Pass / Fail / Pending / Not applicable | `[EV-03]` | `[note]` |

## 4. Evidence summary and claim boundary

### Evidence summary

Explain briefly:

- what was completed;
- which authoritative evidence was reviewed;
- how the evidence satisfies the statement and mapped criteria;
- any important negative or inconclusive result.

### Claim boundary

| Boundary | Statement |
|---|---|
| What this evidence proves | `[state the precise conclusion supported by the evidence]` |
| What this evidence does not prove | `[state related claims that remain unsupported, untested or outside scope]` |

## 5. Authoritative evidence

Use one authoritative source and cross-reference it rather than copying the same artefact into several evidence packs. Use an immutable identifier where it adds value, particularly for binaries, models, workbooks, archives, external packages and experiment outputs.

| Evidence ID | Type | Evidence item | Repository path or external controlled location | Version / commit / run ID | SHA-256 or immutable identifier | Evidence date | What it proves | Status |
|---|---|---|---|---|---|---|---|---|
| EV-01 | Code / document / test / log / screenshot / manifest / dataset / experiment / other | `[item]` | `[relative link or controlled location]` | `[version, commit or run ID]` | `[hash, stable URL or N/A with reason]` | `YYYY-MM-DD` | `[purpose]` | Available / Pending / Gap / Superseded |

## 6. Validation record

| Check | Result | Evidence or note |
|---|---|---|
| Required deliverable exists | Pass / Fail / Pending | `[link or explanation]` |
| Every required criterion is mapped to evidence | Pass / Fail / Pending | `[criterion and evidence IDs]` |
| Definition of Done or acceptance criteria checked | Pass / Fail / Pending | `[review record]` |
| Evidence is version-controlled or independently backed up | Pass / Fail / Pending | `[commit, PR, archive, checksum or backup]` |
| Evidence version, run or integrity identifiers are sufficient | Pass / Fail / Pending | `[note]` |
| Claim boundary is explicit and proportionate | Pass / Fail / Pending | `[note]` |
| Validation independence and approval scope are stated accurately | Pass / Fail / Pending | `[note]` |
| RTM status fields match this evidence record | Pass / Fail / Pending | `[RTM reference or discrepancy]` |
| No unresolved contradiction affects the claim | Pass / Fail / Pending | `[note]` |

**Validation review result:** `[Validated / Partially Validated / Not Validated]`

> `Partially Validated` is a review conclusion only. Until all controlled criteria pass, the RTM Validation field remains `Not Validated` unless the RTM baseline explicitly defines another value.

**Validation conclusion:**  
`[State clearly what is proven, which criteria passed, what remains, and why the chosen validation review result is justified.]`

**Status source-of-truth rule:**  
`[Copy Working status, Validation state and Effective status from the controlled RTM. A record may be called Verified only when the implementation/deliverable exists, the planned evidence exists, the criteria are checked and RTM Validation is Validated.]`

## 7. Traceability

| Relationship | IDs or links |
|---|---|
| Requirement(s) | `[IDs]` |
| Work package(s) | `[IDs]` |
| Engineering practice(s) | `[IDs]` |
| Objective(s) | `[IDs]` |
| Research question(s) | `[IDs]` |
| Test / experiment / evidence IDs | `[IDs]` |
| Change request / decision IDs | `[CR/CHG/ADR IDs or None]` |
| Pull request / commit / release | `[links or IDs]` |

## 8. Limitations, gaps, follow-up and revalidation

### Limitations, gaps and follow-up

- Record every relevant limitation, missing item, rejected assumption, blocker or future action.
- Write `None` only when the validation review found no relevant gap.
- Assign an owner and target date to follow-up work where practical.

### Revalidation triggers

| Trigger | Applies? | Required action / owner |
|---|---|---|
| Parent requirement, Definition of Done or acceptance criterion changes | Yes / No / Pending | `[action]` |
| Affected code, architecture, workflow or controlled document changes | Yes / No / Pending | `[action]` |
| Runtime, model, dependency, dataset or external artefact version changes | Yes / No / Pending | `[action]` |
| Test method, prompt, rubric, metric or processing script changes | Yes / No / Pending | `[action]` |
| Target hardware, operating system or deployment environment changes | Yes / No / Pending | `[action]` |
| New contradictory, negative or superseding evidence is discovered | Yes / No / Pending | `[action]` |

**Revalidation required now:** `Yes / No / Pending`  
**Next review date:** `YYYY-MM-DD / Event-triggered / Not scheduled`

## 9. Change control

Any later change that affects this evidence claim must update, where applicable:

1. the authoritative source or controlled successor;
2. this evidence record and its record version;
3. the criterion-to-evidence mapping;
4. the controlled RTM Working status, Validation state and Effective status;
5. related requirement, work-package and engineering-practice evidence records;
6. affected tests, experiment registers, evidence indexes and report sections;
7. the project change log when the controlled baseline changes;
8. the supersession fields when this record is replaced rather than revised.
