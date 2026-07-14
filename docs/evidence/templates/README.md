# Evidence Template Guidance

**Current template:** [Evidence Record Template v1.1](Evidence-Record-Template.md)  
**Effective from:** 2026-07-14  
**Revision history:** [Evidence Template Revision History](Evidence-Template-Revision-History.md)

Use the common template for requirement, work-package, engineering-practice and experiment evidence packs.

## How to use it

1. Copy the template into the required evidence folder as `README.md`.
2. Replace every placeholder with project-specific information.
3. Copy the exact controlled statement and identify its source baseline or version.
4. Give every acceptance criterion or Definition-of-Done item a local ID such as `AC-01`.
5. Map every required criterion to one or more authoritative evidence IDs such as `EV-01`.
6. Link to the authoritative document, code, test, experiment, log, screenshot, manifest, checksum, commit or pull request.
7. Do not copy the same authoritative artefact into several evidence folders.
8. State precisely what the evidence proves and what it does not prove.
9. Complete each validation check before setting the validation state to `Validated`.
10. Record unresolved gaps honestly as limitations, blockers or follow-up work.
11. Record revalidation triggers so later changes cannot silently invalidate the claim.
12. Increase the evidence-record version whenever the claim, evidence set, validation conclusion or controlled statement changes materially.

## Controlled status rules

The three status fields describe different things:

- **Working status** describes whether the deliverable exists or work is blocked.
- **Validation state** describes how completely the evidence has been reviewed against the criteria.
- **Effective status** is the combined project status and must follow the table below.

| Working status | Validation state | Effective status |
|---|---|---|
| Not Started | Not Validated | Not Started |
| In Progress | Not Validated | In Progress |
| Implemented | Not Validated | Implemented |
| Implemented | Partially Validated | Partially Verified |
| Implemented | Validated | Verified |
| Blocked | Any | Blocked |

Do not use `Verified` merely because a document, folder, code change or test output exists. Verification requires an implemented deliverable and completed validation against the controlled criteria.

## Criterion-to-evidence mapping

Every required criterion must show:

- a stable local criterion ID;
- its exact wording;
- Pass, Fail, Pending or Not applicable;
- the evidence ID or IDs supporting that result;
- a reviewer note when interpretation is needed.

A criterion marked Pass without linked evidence is incomplete. A required criterion marked Pending or Fail prevents full validation unless the controlled acceptance rule explicitly permits partial validation.

## Claim boundaries

Each record must state both:

1. **What this evidence proves** — the narrow conclusion supported by the reviewed evidence.
2. **What this evidence does not prove** — related claims that remain untested, unsupported or outside scope.

Examples:

- A successful model-load smoke test proves basic runtime compatibility; it does not prove model quality, performance stability or TurboQuant activation.
- A completed workflow document proves that the workflow baseline exists; it does not prove that the workflow has been implemented in C#.

## Evidence integrity

Use Git commits and pull requests for repository text and code. Add a version, commit, run ID, checksum or other immutable identifier when the evidence could otherwise change without detection.

A SHA-256 or equivalent identifier is particularly important for:

- model files and datasets;
- executables and external builds;
- Excel workbooks and archives;
- generated result packages;
- large raw logs or outputs stored outside Git.

Write `N/A` only with a brief reason, such as a Git-tracked Markdown file already being identified by its commit.

## Validation independence and approval scope

Record who performed the review and how independent the review was:

- **Self-review** — the owner reviewed their own work;
- **Automated** — a controlled script or CI check performed the validation;
- **Peer** — another project contributor reviewed it;
- **Supervisor** — the academic supervisor reviewed or approved it;
- **External** — an industry mentor or independent technical reviewer reviewed it.

Also state the approval scope accurately. Developer validation must not be described as supervisor or external approval.

## Revalidation

Revalidate a record when a change could affect the evidence claim, including changes to:

- the parent requirement or acceptance criteria;
- affected code, architecture, workflows or controlled documents;
- model, runtime, dependency or dataset versions;
- prompts, rubrics, metrics, tests or processing scripts;
- target hardware, operating system or deployment environment;
- contradictory or superseding evidence.

Revalidation can confirm the existing conclusion, downgrade it, replace it with a superseding record or identify a new gap.

## Migration from template v1.0

Existing evidence records created and validated under template v1.0 are not automatically invalidated.

Apply these rules:

1. All new evidence records must use v1.1.
2. An existing record must migrate to v1.1 when it is materially changed, revalidated or superseded.
3. All active Verified records should be checked for v1.1 compliance during the final release audit.
4. High-risk or release-critical records may be migrated earlier.
5. Do not rewrite historical conclusions merely to make the layout look newer.
6. Preserve the Git history and state the previous record or template version in the metadata.

A folder containing only an empty or generic README is preparation, not completion evidence.
