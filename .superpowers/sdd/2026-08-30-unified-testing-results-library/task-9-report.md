# Task 9 Report: upstream llama.cpp final report

## Status

Implemented and validated the evidence-bound upstream llama.cpp final-results route for WB-01 revision 1.4.

## Source reconciliation and authority

- Reconciled the controlled workbook, revision register, evidence index, historical quality scoring, resource summary, later Test-Run and Performance registers, and the raw-results placeholder.
- The intended matrix is exactly UL-01 through UL-13 and all 13 project workloads are recorded as completed.
- The controlled Evidence Index supplies 642 admitted UL-01 through UL-13 log files. Every repository-relative path exists and every SHA-256 matches; there are zero missing files and zero hash conflicts.
- Fourteen additional controlled/supporting source files are admitted, for 656 normalized evidence records in total.
- The later Test-Run and Performance registers contain no formal UL-01 through UL-13 rows because this campaign predates repetition-level registration. Indexed logs are therefore the repetition authority.
- `experiments/raw-results/upstream-llama-cpp` contains only its README placeholder. No absent raw-result observation or editable source workbook was invented.
- Historical missing fields use the exact display and semantic value `Not collected`; they are never translated to zero.

## Implementation

- Added `scripts/testing/final_results/llama_adapter.py` to normalize and publish the upstream llama.cpp campaign through the common final-results model, schemas, writers, evidence hashing, report model, DOCX renderer, parity checker, and route-manifest machinery.
- Added `scripts/testing/tests/test_final_results_upstream_llama.py` with acceptance, source-mutation, report-mutation, schema, referential-integrity, generated-inventory, and parity coverage.
- Expanded every source benchmark repetition: 41 llama-bench prompt/decode samples and 39 llama-server TTFT/resource samples, producing 80 measurement records.
- Produced 13 attempt records, 65 metric summaries, 54 original quality observations, five canonical nonterminal failure records, 12 scoped deviation records, and 656 evidence records.
- Preserved the original 2026-07-15 adjudication under tracked `GTQ-QUALITY-RUBRIC-v1` and frozen `GTQ-PROMPTS-v1`, including weights, deterministic checks, anchors, caps, procedure, and generation settings. The report explicitly forbids direct quality comparison with OpenVINO or later campaigns that used different methods.
- Bounded the decision labels exactly as required: UL-08 only as best observed CPU, UL-10 only as best observed Intel GPU, and UL-05 only as the recorded fallback.
- Generated the full common route structure and the approved 15-section canonical Markdown report, derivative DOCX and PDF, parity/coverage/data/integrity/visual receipts, evidence and claim maps, reproduction guidance, and checksum manifest.

## TDD evidence

The initial acceptance/mutation run produced the genuine missing-implementation RED:

```text
9 failed
```

After the minimal implementation, the focused suite reached GREEN. Schema and referential-integrity coverage was then added, giving the final focused result:

```text
10 passed in 4.00s
```

## Regression verification

The relevant final-results regression was captured to JUnit and parsed successfully:

```text
216 tests
0 failures
0 errors
0 skipped
211.182 seconds
```

All normalized route records validate against the seven Draft 2020-12 schemas. Attempt, measurement, summary, quality, failure, evidence, and route-manifest references reconcile.

## Report, parity, and Word safety

- Canonical Markdown and generated DOCX semantic parity: Passed (`matches=true`).
- DOCX size: 101,083 bytes.
- PDF size: 1,076,067 bytes.
- The owned Word exporter completed with exit code 0 under the 180-second limit in 15.2 seconds.
- The pre-existing hidden Word baseline was PID 5032 before and after export. No transient owned Word process remained.
- No editable workbook was synthesized because the evidence contains no editable source workbook; the canonical Markdown is the controlled source for this publication.

## PDF and visual validation

- The PDF contains 47 searchable, nonblank pages and all approved report sections.
- Rendered every page with PyMuPDF and inspected six contact sheets covering pages 1-9, 10-18, 19-27, 28-36, 37-45, and 46-47.
- No blank/corrupt page, clipping, truncation, or broken table-header continuation was observed.
- Navy, teal, blue, and explicit status text remain legible. The full evidence index intentionally spans pages 18-46 and remains readable at page zoom.
- Temporary contact sheets were created beneath the OS temporary directory, excluded from the route and manifest, and removed after inspection.

## Manifest and generated inventory

The route contains exactly 45 files. The checksum manifest contains 44 entries, covering every route file except the checksum manifest itself.

```text
manifest entries: 44
route files: 45
exact policy: all route files except evidence/manifest-sha256.txt
hash validation errors: []
```

The generated route includes:

- route README and route manifest;
- protocol plan, execution sequence, intended matrix, metric definitions, and deviations;
- repository, hardware, software, model-artifact, and environment identity;
- canonical Markdown plus generated DOCX and PDF;
- attempts, repetition measurements, summaries, and availability data;
- original quality rubric, prompt suite, scores, calibration, adjudication, and outputs index;
- failure/deviation register and explicit non-duplication guidance for curated logs;
- full evidence index, source locations, claim-evidence map, and checksum receipt;
- reproduction commands, dependencies, and maintained-script pointer;
- coverage, data, parity, integrity, visual, and narrative validation receipts.

## Self-review and concerns

- Five canonical failure records retain only valid test/attempt relationships; all seven historical source items remain in the 12-row nonterminal deviation/precedence ledger.
- Register and raw-results gaps are preserved as limitations and `Not collected`, not silently backfilled.
- The recommendation labels are deliberately narrower than some legacy workbook prose because Task 9 requires only the three evidence-bounded labels above.
- The legacy quality evidence is retained without re-adjudication and cannot support cross-method claims against OpenVINO campaigns.
- The report is long because all 656 admitted evidence records and hashes remain directly auditable.
- Existing unrelated recovered worktree changes were preserved and excluded from the Task 9 staged set.

## Fix round 1

- Added independent source-authority reconciliation. The build now proves zero exact UL-01 through UL-13 rows in both current Test-Run and Performance registers and validates the WB matrix/formal identities and statuses, formal performance/resource/quality values, server and processed-resource medians, prompt-level quality aggregates, and all 642 `(Test_ID, Run_ID, path, SHA-256)` Evidence-Index bindings.
- Added explicit precedence deviations for UL-13 WB formal throughput (`40.789/7.211`) versus indexed median (`40.844/7.212`), UL-05 legacy displayed P1-P6 mean (`5.9`) versus arithmetic prompt mean (`5.750`), and legacy WB decision labels versus the approved bounded UL-08/UL-10/UL-05 labels.
- Comparator-set claims now bind CPU evidence across UL-01 through UL-08 and Intel-GPU evidence across UL-09 through UL-13 rather than citing only the selected row.
- Preserved all seven historical items in `protocol/deviations.csv` with explicit scope type, scope test IDs, nonterminal state, disposition, and evidence. Setup-only and campaign-wide items no longer invent canonical attempts; multi-test UL-F06 is expanded to valid UL-10 and UL-12 relationships.
- Added a computed relationship-validation receipt covering attempt, test, scope, and evidence references. Mutation tests prove invalid register, evidence binding, metric, resource, quality, attempt, scope, and evidence relationships are rejected.
- Bound quality records and generated quality documentation to the tracked rubric and prompt authorities. Unsupported calibration and component increment data use the exact `Not collected` semantics.
- Reproduction now provides five exact ordered commands: portable normalize/render, owned Word export, PDF finalization, manifest validation, and focused validation, with pinned interpreter/dependency paths.
- Fix-round RED: `9 failed, 10 passed in 4.91s`. Dynamic-page validation added a separate expected RED when the revised report grew to 47 pages, and executable-command validation added a final expected RED for the portable interpreter import path. Final focused GREEN: `20 passed in 13.17s`.
- Full final-results regression: `216 passed in 211.22s`; JUnit recorded 216 tests, zero failures, zero errors, zero skips, and 211.182 seconds. The Word baseline remained PID 5032.
