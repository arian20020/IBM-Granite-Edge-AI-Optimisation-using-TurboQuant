# Governance requirement status review

This handover checks G-M01–G-M09 against the retained records at local commit `df820879`, plus the current manual wording edits. It does not replace the controlled requirements matrix or claim that later report edits are already complete.

## Status to use in the report review

| ID | Recommended status | Reason and remaining work |
| --- | --- | --- |
| G-M01 | Completed for the working baseline | The versioned [project definition](../planning/Project-Definition-v1.1.md) records the aim, objectives, research questions, scope and exclusions. This does not claim supervisor approval. |
| G-M02 | Completed for the working baseline; reconcile old summaries | The [validated baseline record](../evidence/requirements/G-M02/README.md) covers stable IDs and acceptance criteria. Older generated summaries still say In Progress. Correct those through the controlled record process; do not treat them as proof that the baseline is missing. |
| G-M03 | Partially completed | The [matrix and reverse indexes](Requirements-Traceability-Matrix.md) exist. A final audit linking every active Must to current implementation, tests and evidence has not been established by this review. |
| G-M04 | Records available; final consistency review outstanding | The [report diagrams](../architecture/diagrams/report/README.md) are retained, but their record explicitly says the final implementation-accuracy review is not included. Check diagrams, decisions, contracts, states and diagnostic codes against the chosen release before marking the whole requirement complete. |
| G-M05 | Completed for the working baseline | The [register validation](../evidence/requirements/G-M05/README.md) records the consolidated control system as verified. This does not mean every risk is closed or that a project licence has been chosen. |
| G-M06 | Partially completed | The [change log](../planning/Change-Log.md) links dated scope decisions. Complete coverage of affected tests, commits and report sections for all approved changes still needs checking. |
| G-M07 | Completed for the tested report-cut-off folders | The [independent recovery record](../evidence/governance/Independent-Evidence-Recovery-Test-20260914.md) covers 226 final-evidence files and 2,482 raw-evidence files restored from OneDrive, with matching sizes and hashes and zero differences. This is a retained owner-run result, not a new recovery test. Later additions need another backup and check. |
| G-M08 | Manuals completed within the stated setup limits; requirement partially completed | The [manuals](../manuals/README.md) cover launch, downloads, use, build, tests and limitations. The final feature-status table still needs reconciliation with the requirements matrix. A separate-PC installation has not been fully verified; keep that limit visible. |
| G-M09 | Partially completed | The [release handover](../../release-evidence/README.md) retains evidence and an executable hash, but does not establish a complete final tagged release, installer checksum, final independent backup and reconciled answers to every research question. No tag points at the reviewed local HEAD. |

## Instructions for the reviewing author

Mark G-M01, G-M02 and G-M05 complete with their working-baseline limits. Update G-M07 to complete for the two tested evidence folders; the raw-evidence recovery is already recorded. Mark the manual-writing part of G-M08 complete, but keep the full requirement open until the feature-status reconciliation is finished.

Do not mark G-M03, G-M04, G-M06, G-M08 or G-M09 fully complete on the strength of manuals alone. Report updates can describe completed work, but cannot replace missing checks. Reconcile stale generated records from their controlled source rather than changing historical evidence silently.

This review changed documentation only. It did not run application tests, change application code, create a release tag or verify a new installer.
