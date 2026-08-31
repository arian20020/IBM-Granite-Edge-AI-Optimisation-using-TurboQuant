# Task 6 Report: Experimental OpenVINO fv6 normalization

## Status

Implemented the experimental OpenVINO adapter and generated the canonical fv6 route at `docs/testing/final-results/04-openvino-experimental-fork`.

## Implementation

- Added `build_experimental_bundle(repo_root)` and `write_experimental_route(repo_root)` in `scripts/testing/final_results/openvino_adapter.py`.
- Restricted the adapter's campaign reads to fv6 detailed, comparison, coverage, quality-detail, rows, raw-result, and prompt-input evidence, plus the already-generated fv6 Excel workbook required for the exact source copy.
- Reconciled the detailed CSV against `rows.json`, the comparison and coverage CSVs, each raw-result hash, three benchmark repetitions per passed case, all quality prompt hashes, and the 48-prompt/three-criterion quality structure.
- Normalized 81 attempts: 27 `passed` and executed, plus 54 `artifact_unavailable` and unexecuted. Unavailable cases produce failure records but no measurement, summary, or quality records.
- Normalized 81 benchmark measurements and 81 derived summaries. Decode throughput is recomputed as the median of three repetitions, TTFT comes from the repetition selected by median decode throughput, and peak working set is recomputed as the worst observed repetition.
- Normalized 3,888 criterion-level quality records: 27 passed cases x 48 prompts x 3 weighted criteria. Category and criterion are both retained in `criterion_id`; prompt suite, weighted rubric, scoring version, raw evidence, prompt inputs, and output hashes remain traceable.
- Inventoried 81 source evidence records: six campaign/workbook sources, 27 raw case results, and 48 prompt inputs.
- Generated canonical attempts, measurements, summaries, availability, quality, failures, system/repository, evidence, claims, and validation artifacts.
- Copied `Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx` byte-for-byte into the route source-results folder.
- Generated a 21-entry route checksum manifest through the shared non-destructive provenance API.

## TDD evidence

Initial acceptance RED after adding the test and before creating the adapter:

```text
ModuleNotFoundError: No module named 'scripts.testing.final_results.openvino_adapter'
1 error in 0.24s
```

The first end-to-end GREEN run reported:

```text
1 passed in 2.62s
```

A second focused test was then added to catch destructive recreation of an identical existing manifest. Before the correction it failed with:

```text
assert 1788140312606680700 == 1700000000000000000
1 failed in 2.16s
```

After removing the unconditional manifest recreation, the two focused tests passed:

```text
2 passed in 4.58s
```

The final focused run after refactoring reported:

```text
2 passed in 4.64s
```

## Verification

The non-Word Tasks 1-6 final-results suite passed:

```text
71 passed in 5.45s
```

The first complete Tasks 1-6 run reported 82 passed and one Task 5 Word-process baseline failure. That test's captured baseline included transient WINWORD PIDs 8616 and 19048 that had exited by its final equality assertion; it ended with the preserved pre-existing PID 5032. No Task 6 assertion failed.

At a confirmed stable read-only Word baseline of `{5032}`, the single affected Task 5 test was rerun once and passed:

```text
1 passed in 50.08s
```

The read-only Word baseline after that rerun was again exactly `{5032}`. No process was terminated manually.

Independent generated-data verification reported:

```text
attempts 81 {('passed', 'true'): 27, ('artifact_unavailable', 'false'): 54}
measurements 81 summaries 81 scores 3888 failures 54
prompts 48 outputs 1296
workbook_bytes_equal True sha256 09820a5b19edb11efd7640f1d9a13802b82fb464ab6e14622998d2565c5aca83
manifest_errors []
coverage {'artifact_unavailable': 54, 'cache_format_count': 9, 'executed': 27, 'model_weight_artifact_count': 3, 'passed': 27, 'planned': 81, 'quality_prompts_per_passed_case': 48, 'valid': True}
```

`compileall` and `git diff --check` completed with no output or errors for the Task 6 files.

## Generated file inventory

- `route-manifest.json`
- `protocol/intended-test-matrix.csv`
- `system/repository.json`
- `system/hardware.json`
- `system/software.json`
- `system/model-artifacts.csv`
- `results/attempts.csv`
- `results/measurements.csv`
- `results/summary-results.csv`
- `results/availability-matrix.csv`
- `results/source/Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx`
- `quality/prompt-suite.csv`
- `quality/scores.csv`
- `quality/outputs-index.csv`
- `failures/failure-register.csv`
- `evidence/evidence-index.csv`
- `evidence/source-locations.csv`
- `evidence/claim-evidence-map.csv`
- `evidence/manifest-sha256.txt`
- `validation/coverage-validation.json`
- `validation/data-validation.json`
- `validation/integrity-validation.json`

## Self-review

- Source case IDs, statuses, execution flags, configuration identities, failure reasons, raw paths, and raw hashes are checked before normalization.
- The three available model-weight artifacts are identified from source model hashes and sizes; the other six model-weight combinations remain explicitly unavailable.
- No absolute model path is exported. Only the portable artifact label, source hash, and size are retained.
- Nullable/unobserved values are never converted to zero, and unavailable cases do not enter measurements, summaries, quality scores, or outputs.
- All route evidence paths are repository-relative POSIX paths with verified SHA-256 hashes.
- The generated workbook copy matches the source bytes exactly.
- Only Task 6 implementation, test, generated route, and report files are intended for staging; unrelated recovered work remains untouched.

## Concerns

- The fv6 evidence does not contain a portable host-hardware manifest. `system/hardware.json` therefore records `not_collected` with an explicit reason instead of inferring machine identity.
- The earlier complete-suite Task 5 failure was environmental process-baseline volatility. Its isolated rerun from a stable `{5032}` baseline passed, but the full Word suite was not rerun a third time because it creates additional COM lifecycle opportunities without increasing Task 6 coverage.

## Fix round 1/5

### Reviewer findings addressed

- Detailed and comparison sources now reconcile one-to-one by model, weight, and cache identifiers. All shared fields are compared: identifiers, status, executed flag, decode TPS, TTFT, peak working set, KV size, quality score, and failure reason. Numeric blanks become `None`, numeric values are compared after numeric parsing, and booleans are parsed through the controlled source parser.
- Each selected benchmark object must exactly equal one of the three full repetition objects and must be the median-decode repetition. Every repetition result must exactly equal its JSON stdout payload. Detailed selected metrics and the max/min repetition-envelope aggregates are independently recomputed.
- Raw quality evidence now reconciles by case, prompt, category, and criterion. The adapter checks the suite, domain, prompt length, prompt score, case score, output-valid and critical-failure flags, three distinct criterion identities, category, criterion ID, kind, weight/maximum, critical flag, pass flag, awarded points, expected value, observed value, reason, category score, health checks, answer text, and stdout/result identity.
- Generated measurement, score, output-hash, list-valued CSV, and workbook rows have end-to-end round-trip assertions against independent raw evidence reads.
- Coverage and data validation receipts now expose seven and sixteen named checks respectively. Each check records `actual`, `expected`, and `passed`; receipt validity is computed with `all(check["passed"])`. A synthetic bundle missing all measurements proves that data validity becomes false while coverage remains independently true.
- Added `reproduction/README.md` with the canonical regeneration command, frozen evidence paths and hashes, non-fabrication rules, and the manifest update boundary.
- The exact route manifest was deliberately regenerated once for the expanded 22-file set. Routine identical reruns remain non-destructive and preserve the existing manifest.

### Additional RED evidence

The initial reviewer-fix mutation run reported:

```text
15 failed, 1 passed in 18.94s
```

The failures showed that the original adapter accepted a conflicting comparison metric, a selected-repetition metric conflict, six criterion identity/value/flag/reason conflicts, five prompt metadata/score/validity conflicts, an output-text conflict, and validation receipts without named check details.

The validation-receipt API test independently failed at collection before implementation:

```text
ImportError: cannot import name 'build_experimental_validation_receipts'
1 error in 0.17s
```

### GREEN evidence

The expanded source-conflict mutation set passed:

```text
21 passed, 3 deselected in 4.34s
```

The complete Task 6 suite passed:

```text
24 passed in 11.34s
```

Relevant non-Word Tasks 1-6 regressions passed:

```text
93 passed in 10.68s
```

`compileall` completed successfully and `git diff --check` reported no whitespace errors. Word/PDF tests were not rerun because this fix does not change their code or process behavior.

### Independent integrity audit

```text
five frozen fv6 source hashes unchanged
workbook byte identity 09820a5b19edb11efd7640f1d9a13802b82fb464ab6e14622998d2565c5aca83
manifest entries 22 errors []
receipt checks 7 16 valid True True
```

### Task 8 integration boundary

The current checksum manifest is exact for Task 6's 22-file route set and the shared manifest API correctly refuses to overwrite a different existing receipt. Task 8 is expected to add generated report/workbook artifacts. Once its planned file set is complete, Task 8 must perform one deliberate manifest regeneration and validate the resulting exact set; it must not silently leave those added artifacts outside the receipt.

## Fix round 2/5

### Reviewer findings addressed

- Coverage receipts now derive the exact nine cache-format identities, exact three executed model/weight artifact identities, exact 54 unavailable case identities, and per-attempt source semantics from the frozen detailed-results authority. Artifact-unavailable coverage requires both the canonical `artifact_unavailable` status (sourced from `model_artifact_unavailable`) and `executed=false`; a generic not-executed state no longer qualifies.
- Data receipts enforce uniqueness for attempt, measurement, summary, quality, failure, evidence, prompt, and output identifiers. Generated outputs now carry the canonical stable ID `<case>--<prompt>--output`.
- Referential checks are record-type, evidence-role, and case aware. Measurements must link to a passed attempt and raw evidence for the same case; summaries must link to compatible same-case measurements; quality and outputs must link to same-case raw evidence; unavailable failures must link to a same-case unavailable attempt and the two authoritative tabular sources; prompt inputs must match the prompt evidence path and hash; source evidence cannot masquerade as derived evidence.
- Raw quality scoring schema is checked uniformly across all 1,296 prompt runs. `QualityRecord.scoring_version` is derived from the validated frozen schema rather than independently hard-coded at record creation.
- Added count-preserving and cross-link mutation tests for unsupported cache identities, wrong executed artifacts, duplicate identifiers, cross-case attempt/measurement/evidence links, wrong evidence roles, derived-source evidence, and non-uniform scoring schema.
- Added independent source mutations proving that every repetition stdout must equal its result (including non-selected repetitions), the selected benchmark must remain the complete median-decode repetition, and synchronized raw/embedded prompt metadata changes are still rejected against the independent quality-detail CSV.
- Deliberately regenerated the validation receipts, outputs index, and exact 22-entry checksum manifest after the planned schema and receipt changes. The frozen evidence and source workbook were not changed.

### RED and intermediate evidence

Before the round-two implementation, the expanded focused suite reported:

```text
18 failed, 28 passed in 27.48s
```

Those failures independently exposed the missing output identifier, inferred rather than exact unavailable semantics, count-only cache/artifact checks, missing ID uniqueness, global rather than typed/case-aware foreign keys, role-insensitive evidence validation, and unvalidated scoring schema.

After implementing those contracts, one focused run reported:

```text
44 passed, 2 failed in 25.86s
```

Both remaining failures identified a writer-only CSV field placement mistake: `output_id` was computed in output rows but had been placed in the availability header rather than the outputs-index header. The writer field lists were corrected without changing the canonical data model.

### Final GREEN evidence

The complete focused Task 6 suite passed:

```text
46 passed in 27.77s
```

The relevant file-generation/non-Word final-results regression suite passed:

```text
115 passed in 27.25s
```

Word/PDF automation was not run because this fix does not touch its code or process lifecycle.

### Final integrity audit

```text
attempts 81: passed 27, artifact_unavailable 54
outputs 1296; unique output_id values 1296
coverage receipt checks 10/10 passed; valid true
data receipt checks 25/25 passed; valid true
manifest entries 22; validation errors []
workbook source/copy byte-identical; 663856 bytes
workbook SHA-256 09820a5b19edb11efd7640f1d9a13802b82fb464ab6e14622998d2565c5aca83
```

The five frozen fv6 evidence hashes remain `016d9424...`, `ebf8a968...`, `2f573909...`, `834c2caa...`, and `ca3f4af1...`, matching the pre-fix audit.

### Remaining integration boundary

The checksum manifest remains intentionally exact for the current 22-file Task 6 route. If Task 8 adds generated report or workbook artifacts, its planned output set must be finalized first and the manifest must then be deliberately regenerated and validated. The current non-destructive manifest API is expected to reject a changed file set unless that explicit regeneration step is performed.

## Fix round 3/5

### Reviewer findings addressed

- Added source-derived canonical identity bindings for attempts, measurements (including run and repetition identity), summaries, criterion-level quality records, failures, evidence, prompts, and outputs. Constructors use the same declared ID helpers, while receipt expectations are keyed from frozen detailed, quality-detail, raw-result, prompt-file, and workbook entities so synchronized record permutations cannot validate themselves.
- Evidence identity expectations reproduce the canonical compact digest ID and full-digest collision fallback in the frozen source construction order. Each evidence ID is bound to the expected route, campaign, role, repository-relative path, SHA-256, size, source label, and non-derived provenance state.
- Prompt provenance binds each published prompt ID to the exact frozen suite, domain, length class, prompt-file evidence ID, repository-relative path, and text-content SHA-256. Quality records must reference a prompt that exists in this published suite and retain the frozen prompt suite and same-case raw-result evidence.
- Output provenance binds each canonical output ID to the exact case/prompt raw run, same-case raw-result evidence, domain, prompt length, status, output-valid and critical-failure flags, prompt score, and SHA-256 recomputed from the raw answer text.
- All three summaries for every passed case now retain the exact ordered three benchmark repetition measurement IDs. Receipt validation recomputes the expected median decode throughput, TTFT selected by median decode throughput, and worst observed peak working set directly from frozen raw repetitions, and validates the metric-specific ID, unit, aggregation rule, value, and lineage.
- Attempt ID uniqueness now compares the unique-ID count with the actual attempt-record count. The independent planned-count coverage check continues to require 81 attempts.
- Added explicit named receipt checks for all canonical identifier bindings, prompt/quality/output source bindings, and exact summary lineage/value validation. The data receipt now exposes 37 named checks; validity remains `all(check["passed"])`.

### RED evidence

Before production changes, the expanded focused suite reported:

```text
16 failed, 45 passed in 39.28s
```

The failures showed the existing one-measurement TTFT/peak lineage, missing canonical binding checks, hard-coded attempt uniqueness expectation, acceptance of a nonexistent quality prompt, acceptance of a count-preserving synchronized prompt/output ID swap, and missing exact summary lineage/aggregation/value validation.

A follow-up mutation strengthened the synchronized permutation contract to require the canonical prompt, output, and quality-prompt identity bindings themselves to fail, not only the source-binding checks. It produced the expected focused RED:

```text
1 failed in 1.40s
```

### GREEN evidence

The receipt and mutation subset passed:

```text
33 passed, 28 deselected in 35.79s
```

After deliberate route regeneration, the complete Task 6 focused suite passed:

```text
61 passed in 45.01s
```

The strengthened synchronized-permutation test passed independently:

```text
1 passed in 1.18s
```

The relevant final-results/file-generation regression suite passed:

```text
130 passed in 53.22s
```

### Generated receipt and integrity audit

```text
data receipt checks 37/37 passed; valid true
coverage receipt checks 10/10 passed; valid true
summaries 81; all 81 contain exactly three ordered source measurement IDs
manifest entries 22; validation errors []
workbook source/copy byte-identical
workbook SHA-256 09820a5b19edb11efd7640f1d9a13802b82fb464ab6e14622998d2565c5aca83
```

The fixed-mtime idempotence test can make Git's stat cache report the regenerated checksum manifest as unchanged even when its bytes differ from `HEAD`. The manifest is therefore explicitly force-hashed with `git add` during Task 6 staging, and its staged summary/data receipt digests are reviewed before commit.

### Task 8 integration boundary

The route manifest remains an exact receipt for the 22 Task 6 files. Task 8 must deliberately regenerate and validate it after finalizing any added report/workbook artifact set; those later artifacts must not be silently omitted from this provenance boundary.

## Fix round 4/5

### Reviewer findings addressed

- Added one source-derived full-entity-set equality check for every canonical type published by Task 6: attempts, measurements, summaries, quality, failures, evidence, prompt rows, output rows, availability rows, and model-artifact rows.
- Full comparisons use canonical serialization semantics: controlled status strings, booleans, nullable scalars, integer token/memory counts, floating-point measurements, and list-valued evidence/lineage fields. Every published route/campaign/status/execution/reason/stage/source-status/failure-kind/metric/rubric/schema/path/hash/role/input field is included for its record type.
- The comparison helper checks key multiplicity before keyed equality, so duplicate entities cannot be silently collapsed by dictionary construction. Receipt diagnostics report compact duplicate, missing, unexpected, or mismatched-field messages without embedding thousands of full rows.
- Expected attempt and failure rows are reconstructed from frozen detailed results and source-evidence identities, including exact status, execution flag, failure reason/stage, source status, and ordered evidence IDs.
- Expected measurement rows parse each frozen repetition stdout, require exact stdout/result equality, and bind all latency, throughput, peak-memory, and token values to the canonical case/repetition identity and raw evidence.
- Expected quality rows originate from each frozen raw criterion entity and are independently reconciled with the quality-detail CSV projection. The published record binds criterion identity, awarded/maximum score, suite, rubric, scoring schema, and same-case raw evidence. Source-only criterion flags, expected/observed payloads, and reasons remain enforced by the pre-normalization raw-to-quality-detail reconciliation; they are not falsely claimed as fields in the shared `QualityRecord` schema.
- Expected evidence rows reproduce every published provenance field, including collision-aware stable ID, role, relative path, digest, size, label, derived flag, and input IDs. Prompt and output full rows retain the exact file/output hashes and all published metadata.
- Availability and model-artifact rows are now explicit validation inputs. The writer passes the exact rows it publishes; direct receipt callers receive deterministic rows generated from the bundle/source when omitted.

### RED evidence

The expanded focused suite reported before implementation:

```text
13 failed, 60 passed in 58.40s
```

The failures independently demonstrated acceptance of a synchronized three-repetition identity rotation, a direct measurement value mutation, a synchronized same-prompt criterion/quality-ID swap with differing maximum score, attempt and failure reason/stage/status mutations, evidence size/input mutations, and fabricated availability/model-artifact reasons. The end-to-end receipt assertion also proved all ten named checks were absent.

### GREEN evidence

The new full-entity and mutation subset passed:

```text
21 passed, 52 deselected in 24.15s
```

After deliberate receipt/manifest regeneration, the complete focused suite passed:

```text
73 passed in 64.46s
```

After the source-expectation self-review refinements, the targeted subset and complete focused suite passed again:

```text
21 passed, 52 deselected in 23.51s
73 passed in 60.38s
```

The relevant final-results/file-generation regression suite passed before the source-only expectation refactor:

```text
142 passed in 70.41s
```

### Receipt and provenance audit

```text
coverage receipt checks 10/10 passed; valid true
data receipt checks 47/47 passed; valid true
full entity-set checks 10/10 passed
manifest entries 22; validation errors []
```

The shared quality schema intentionally contains only the canonical fields listed in `QualityRecord`; Task 6 does not expand that cross-route schema. Detailed criterion flags/reasons are still verified against raw evidence before canonical records are built, while the receipt's full-entity comparison covers every field actually published in `quality/scores.csv`.

### Task 8 integration boundary

The checksum manifest remains exact for the 22 current route files. Task 8 must deliberately regenerate and validate it after its final report/workbook file set is known.
