# Task 9 Report: upstream llama.cpp final report

## Status

Implemented and validated the evidence-bound upstream llama.cpp final-results route for WB-01 revision 1.4.

## Source reconciliation and authority

- Reconciled the controlled workbook, revision register, evidence index, historical quality scoring, resource summary, later Test-Run and Performance registers, and the raw-results placeholder.
- The intended matrix is exactly UL-01 through UL-13 and all 13 project workloads are recorded as completed.
- The controlled Evidence Index supplies 642 admitted UL-01 through UL-13 log files. Every repository-relative path exists and every SHA-256 matches; there are zero missing files and zero hash conflicts.
- Nine additional controlled source files are admitted, for 651 normalized evidence records in total.
- The later Test-Run and Performance registers contain no formal UL-01 through UL-13 rows because this campaign predates repetition-level registration. Indexed logs are therefore the repetition authority.
- `experiments/raw-results/upstream-llama-cpp` contains only its README placeholder. No absent raw-result observation or editable source workbook was invented.
- Historical missing fields use the exact display and semantic value `Not collected`; they are never translated to zero.

## Implementation

- Added `scripts/testing/final_results/llama_adapter.py` to normalize and publish the upstream llama.cpp campaign through the common final-results model, schemas, writers, evidence hashing, report model, DOCX renderer, parity checker, and route-manifest machinery.
- Added `scripts/testing/tests/test_final_results_upstream_llama.py` with acceptance, source-mutation, report-mutation, schema, referential-integrity, generated-inventory, and parity coverage.
- Expanded every source benchmark repetition: 41 llama-bench prompt/decode samples and 39 llama-server TTFT/resource samples, producing 80 measurement records.
- Produced 13 attempt records, 65 metric summaries, 54 original quality observations, seven historical failure/deviation records, and 651 evidence records.
- Preserved the original `upstream-llama-quality-2026-07-15` conservative manual method and strict format caps. The report explicitly forbids direct quality comparison with OpenVINO or later campaigns that used different methods.
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
206 tests
0 failures
0 errors
0 skipped
208.910 seconds
```

All normalized route records validate against the seven Draft 2020-12 schemas. Attempt, measurement, summary, quality, failure, evidence, and route-manifest references reconcile.

## Report, parity, and Word safety

- Canonical Markdown and generated DOCX semantic parity: Passed (`matches=true`).
- DOCX size: 99,003 bytes.
- PDF size: 1,047,823 bytes.
- The owned Word exporter completed with exit code 0 under the 180-second limit in 15.2 seconds.
- The pre-existing hidden Word baseline was PID 5032 before and after export. No transient owned Word process remained.
- No editable workbook was synthesized because the evidence contains no editable source workbook; the canonical Markdown is the controlled source for this publication.

## PDF and visual validation

- The PDF contains 45 searchable, nonblank pages and all approved report sections.
- Rendered every page with PyMuPDF and inspected five 3-by-3 contact sheets covering pages 1-9, 10-18, 19-27, 28-36, and 37-45.
- No blank/corrupt page, clipping, truncation, or broken table-header continuation was observed.
- Navy, teal, blue, and explicit status text remain legible. The full evidence index intentionally spans pages 16-44 and remains readable at page zoom.
- Temporary contact sheets were created beneath the OS temporary directory, excluded from the route and manifest, and removed after inspection.

## Manifest and generated inventory

The route contains exactly 44 files. The checksum manifest contains 43 entries, covering every route file except the checksum manifest itself.

```text
manifest entries: 43
route files: 44
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

- The seven failure records are historical failures/deviations, not terminal failures of the 13 completed workloads; they retain explicit non-terminal semantics.
- Register and raw-results gaps are preserved as limitations and `Not collected`, not silently backfilled.
- The recommendation labels are deliberately narrower than some legacy workbook prose because Task 9 requires only the three evidence-bounded labels above.
- The legacy quality evidence is retained without re-adjudication and cannot support cross-method claims against OpenVINO campaigns.
- The report is long because all 651 admitted evidence records and hashes remain directly auditable.
- Existing unrelated recovered worktree changes were preserved and excluded from the Task 9 staged set.
