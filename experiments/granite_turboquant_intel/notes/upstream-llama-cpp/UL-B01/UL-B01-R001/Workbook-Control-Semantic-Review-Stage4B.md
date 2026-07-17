# Workbook Control Semantic Review - Stage 4B

| Field | Value |
|---|---|
| Run ID | UL-B01-R001 |
| Failure ID | FAIL-CTRL-001 |
| Recorded UTC | 2026-07-14T16:28:43.839881+00:00 |
| Review package | `Stage4-Final-Diff-Review-20260714-170949.zip` |
| Semantic review result | CORRECTION REQUIRED |
| Current classification | Blocked pending Stage 4B regeneration and validation |

## Findings

1. WB-01, WB-02 and WB-03 used `Source file` plus an embedded hash beside
   the controlled output filename. Those values referred to older generated
   copies, became stale and conflicted with the rule that generated DOCX hashes
   are controlled only by `Controlled-Workbook-Manifest.csv`.
2. WB-02 stored a 62-character `Source_SHA256` in the controlled manifest,
   while `Source-Document-Manifest.csv` retained the correct 64-character
   source hash.
3. The Stage 4 validator did not validate `Source_SHA256`, cross-check the
   source-document manifest or reject legacy embedded output-hash metadata.
4. WB-03 used `TQ3 0` rather than `TQ3_0` in its visible first heading.

## Prepared correction

- Standardized the first-page metadata of WB-01 to WB-03.
- Restored the WB-02 source hash from the source-document manifest.
- Corrected the WB-03 visible title.
- Installed a validator that checks source hashes and metadata policy.
- Required fresh two-run deterministic generation, hash baselining, validator
  execution and visual reinspection.

## Boundary

The earlier Stage 4 visual inspection remains valid evidence for the earlier
candidate, but its WB-01 to WB-03 hashes are superseded by this content
correction. No runtime or model test result is affected.


## Stage 4B deterministic correction result

| Field | Value |
|---|---|
| Completed UTC | 2026-07-14T16:29:04.013120+00:00 |
| Two-run complete-file comparison | PASS |
| Source-manifest cross-check | PASS |
| Canonical metadata policy | PASS |
| Detailed workbook validator | PASS pending log capture |
| Controlled workspace validator | PASS pending log capture |
| Current classification | Blocked pending WB-01 to WB-03 visual reinspection |

The manifest now records the newly generated deterministic hashes. WB-04 to
WB-06 are expected to retain their earlier hashes because their canonical
templates and revision histories were unchanged.


## Stage 4B trailing-whitespace recovery

| Field | Value |
|---|---|
| Recorded UTC | 2026-07-14T16:34:12.981780+00:00 |
| Trigger | Final `git diff --check` |
| Affected templates | WB-01, WB-02 and WB-03 |
| Metadata lines corrected | 9 |
| Generated DOCX files edited | No |
| Canonical template hashes recalculated | Yes |
| Current classification | Blocked pending validators and visual reinspection |

The Stage 4B generator and validators had already completed. The workflow
stopped because Markdown hard-break spaces were treated as trailing whitespace
by Git. The spaces were unnecessary for DOCX generation because every metadata
line is converted into its own paragraph.


## Final Stage 4B semantic diff review

| Field | Value |
|---|---|
| Recorded UTC | 2026-07-14T17:08:52.852622+00:00 |
| Review package | `Stage4B-Final-Semantic-Diff-Review-20260714-174613.zip` |
| Changed files reviewed | 46 |
| Manifest hash verification | PASS |
| Git diff check | PASS |
| WB-01 to WB-03 visual reinspection | PASS |
| Total pages reinspected | 19 |
| Final semantic result | PASS |
| Current classification | Blocked pending controlled commit, push, PR and merge |

The final review confirmed that the source hashes, canonical metadata,
generated-DOCX hashes, revision histories, validator behaviour and visual
outputs are internally consistent. No further workbook content correction was
identified before commit.

## Pull-request lifecycle update

Draft PR #21 was created from
`testing/workbook-control-baseline-repair` at commit
`c8ef758be14d863e5fb49fd8e24d61821ace66ca`. No new semantic defect was
identified during this lifecycle update.
