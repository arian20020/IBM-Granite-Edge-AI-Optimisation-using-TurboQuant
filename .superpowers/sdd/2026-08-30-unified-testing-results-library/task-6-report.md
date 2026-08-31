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
