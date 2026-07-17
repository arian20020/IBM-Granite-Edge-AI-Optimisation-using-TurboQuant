# Source-of-Truth Rules

The project uses several connected systems. Each system must have a clear responsibility so the same information is not edited independently in multiple places.

## Authority table

| Information | Authoritative location |
|---|---|
| Problem, aim, RQs, objectives, scope, exclusions and satisfactory outcome | `docs/planning/Project-Definition-v1.md` and controlled successors |
| Requirement and task definitions | Controlled RTM workbook |
| Working status, formal validation and deadlines | RTM Task Checklist |
| Dashboard calculations | RTM workbook |
| Repository task snapshot | Generated `docs/traceability/data/task-catalogue.json` and Markdown views |
| Requirement acceptance evidence | RTM evidence path and the linked controlled evidence record |
| Day-to-day work assignment and discussion | GitHub work-package issue |
| Reviewed code and documentation change | Pull request and commits |
| Baseline changes | Project Definition, Change Log, RTM and affected evidence records |
| Experiment truth | Preserved raw output, manifest, processed result and validation record |

## Edit rules

### Edit the RTM when

- a task statement changes;
- a deadline changes;
- working status changes;
- validation changes;
- an evidence path changes;
- a requirement is added, deferred, superseded or excluded.

After editing the RTM, regenerate the repository snapshot.

### Edit an evidence record when

- new proof is collected;
- a Definition of Done check passes or fails;
- a test or experiment is rerun;
- a limitation or evidence gap is found;
- validation is completed.

### Edit a GitHub issue when

- assigning work;
- discussing implementation;
- recording a blocker;
- planning sub-tasks;
- linking the implementation pull request.

Do not use an issue checkbox as the formal requirement-verification record.

## Status boundary

| State | Meaning |
|---|---|
| Implemented | The code or required document exists. |
| Validated | The stated Definition of Done or acceptance evidence was checked. |
| Verified | Implementation exists and validation is complete. |
| Partially Verified | Some acceptance evidence passes, but meaningful gaps remain. |

Closing an issue means the operational work item is finished. It does not override RTM validation.

## Generated-file rule

Files in `docs/traceability/generated/` and `docs/traceability/data/task-catalogue.json` are outputs of the controlled generator.

Do not repair generated content manually. Correct the workbook or generator, regenerate, validate and review the Git diff.

## Change-control rule

A change affecting a controlled baseline must update all affected records:

1. authoritative source document;
2. project Change Log;
3. RTM definition, status or relationship;
4. generated traceability snapshot;
5. evidence record;
6. affected GitHub issue or pull request;
7. report section where the claim is used.
