# Evidence Template Revision History

**Document ID:** LOG-EVID-TPL-001  
**Status:** Controlling record  
**Owner:** Arian B  
**Last reviewed:** 2026-07-14

This record preserves the history of the common evidence-record structure. Template changes improve evidence governance; they do not by themselves validate a task or change a requirement's completion status.

| Template version | Effective date | Change | Migration rule | Related decision |
|---|---|---|---|---|
| 1.0 | 2026-07-14 | Established the common nine-section evidence record covering metadata, statement, Definition of Done, evidence summary, authoritative evidence, validation, traceability, limitations and change control. | Existing records followed the original common structure. | CR-015 / CHG-015 evidence-conformance work |
| 1.1 | 2026-07-14 | Added explicit template and record versioning, source-baseline identification, criterion-to-evidence mapping, claim boundaries, stronger evidence-integrity identifiers, validation independence, approval scope, controlled effective-status guidance, revalidation triggers and supersession fields. | Mandatory for new records at the time of release. Existing v1.0 records migrate when materially changed, revalidated or superseded. | CR-016 / CHG-016; PR #19 |
| 1.1.1 | 2026-07-14 | Corrected the status fields and guidance so Working status, Validation state and Effective status mirror the authoritative RTM vocabulary instead of introducing a competing local status model. Clarified that `Partially Validated` is a narrative review conclusion, not a third RTM Validation value. | Mandatory for new records after this patch. Existing v1.0/v1.1 records remain valid when sound and migrate on material change, revalidation, supersession or final release audit. | CR-017 / CHG-017 |

## Current compatibility decision

1. The nine-section structure is retained so existing evidence remains familiar and reviewable.
2. No requirement, work package or engineering-practice status changes merely because the template version changed.
3. The controlled RTM remains authoritative for Working status, Validation and Effective status.
4. A v1.0 or v1.1 record remains acceptable when its evidence and validation remain sound.
5. Migration must not fabricate historical metadata, hashes, independent approval or review that did not occur.
6. New information added during migration must be supported by current reviewable evidence.
7. Final release audit must identify any active Verified record that still lacks a clear criterion-to-evidence mapping or claim boundary.

## Change rule

Future template revisions must:

- append a new version row rather than rewrite earlier history;
- record the rationale and migration impact;
- update the template guidance and project evidence index;
- preserve RTM status vocabulary unless an approved RTM change is made first;
- use change control when the revision changes mandatory evidence-governance rules.
