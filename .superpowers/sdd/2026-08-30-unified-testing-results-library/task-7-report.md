# Task 7 Report: Official OpenVINO fv2/fv1 normalization

## Status

Implemented the official OpenVINO adapter and generated the canonical route at `docs/testing/final-results/05-openvino-official-upstream`.

## Implementation

- Added `build_official_bundle(repo_root)`, `build_official_validation_receipts(repo_root, bundle, ...)`, and `write_official_route(repo_root)` in `scripts/testing/final_results/openvino_adapter.py`.
- Kept fv2 consolidated detailed/comparison/coverage/rows evidence authoritative for all 45 final attempts: 15 `passed`, 5 `conversion_failed`, and 25 `hardware_preflight_blocked`.
- Joined fv1 performance, repetition, quality, prompt, and output evidence only to the 15 fv2-passed cases. A passed fv2 case without fv1 raw evidence is rejected. Any published observation on a non-passed fv2 row is rejected.
- Preserved the exact fv2 failure status, stage, and reason. The five 3B FP16 conversion failures link to the final guarded-retry-002 `conversion_failed` manifest; the 25 preflight blocks link to their final model/weight manifests. Earlier/intermediate attempt manifests remain separately indexed.
- Restricted the official matrix to the frozen five cache formats; the only TurboQuant formats are `tbq3` and `tbq4`. PolarQuant and QJL are rejected.
- Reconciled each passed case against three raw benchmark repetitions, the complete selected median-decode repetition, repetition stdout, all selected/envelope metrics, 48 quality prompts, three weighted criteria per prompt, prompt metadata and hashes, output text and stdout, objective scoring schema, and case-level quality score.
- Normalized 45 attempts, 45 measurements, 45 summaries, 2,160 criterion-level quality records, and 30 failures. Non-passed cases publish no performance or quality observations.
- Added source-rebuilt full-entity validation for attempts, measurements, summaries, quality, failures, evidence, prompts, outputs, availability, and model artifacts, plus exact same-case typed references and non-passed observation exclusions.
- Copied `Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx` byte-for-byte as the primary workbook. Indexed the original fv1 workbook as prior evidence without copying it.
- Generated the common canonical route structure, reproduction instructions, coverage/data receipts, and a non-destructive SHA-256 manifest.
- Parameterized only the route/campaign fields of the reviewed shared benchmark-normalization helper; experimental callers retain their original defaults.

## TDD evidence

The first test invocation exposed only missing portable-interpreter import-path setup:

```text
ModuleNotFoundError: No module named 'scripts'
1 error in 0.15s
```

After correcting the test harness without production changes, the genuine feature RED was:

```text
ImportError: cannot import name 'build_official_bundle'
1 error in 0.21s
```

The first post-implementation focused run reported:

```text
2 passed, 11 failed in 1.95s
```

Those failures exposed duplicate byte-identical source-inventory content, synchronized mutation-fixture requirements, dangling preflight-input symlinks in isolated fixtures, and a non-JSON set in a validation receipt. Corrections retained all distinct attempt manifests, content-deduplicated identical source inventories, synchronized source mutations across authorities, excluded only unused dangling links from test clones, and published sorted receipt values.

The focused suite then progressed through 8 passed/5 failed and 12 passed/1 failed before reaching:

```text
13 passed in 10.54s
```

Expanded full-entity mutation coverage for quality, failure, summary value/lineage, prompt, output, availability, and model-artifact rows passed:

```text
14 passed in 15.63s
```

## Verification

The official, complete experimental, models, Markdown, evidence, and CSV non-Word final-results suite passed before the final mutation-only test expansion:

```text
149 passed in 74.48s
```

The final fresh combined regression run after the expanded mutation coverage passed:

```text
150 passed in 81.97s
```

Independent generated-data and schema verification reported:

```text
attempts 45: passed/true 15, failed/false 5, blocked/false 25
measurements 45; summaries 45; quality 2160; failures 30
prompts 48; outputs 720
coverage receipt valid true; data receipt valid true
Draft 2020-12 schema errors 0 across 2,416 canonical records
manifest entries 22; validation errors []
source locations 88
```

Workbook verification:

```text
primary/copy SHA-256 1d5fc2893e0c7f412140b3fa1a26c4a0c18e3c65ecfa356e80549dc4cd10aff7
primary byte identity true
prior fv1 SHA-256 b3e26eae69c3854dec26536c6d141943572292f8a9ea331cb4de1d88c76b32b4
prior workbook duplicate copy exists false
```

The four frozen authoritative CSV hashes remain unchanged from the pre-implementation audit:

```text
fv2 detailed   ecd7ea7c9e71d235d39c4bd29c00b52a7bc4ccdb2b151b7cfe9b6ba4e84c9116
fv2 comparison bc078d9f96b033a4e81dbfcefec4e87c788d9673c567189ef5e8215a7dece0fe
fv1 detailed   cc594ccd64d93dee5b14424bc2b8746f41999b1e324d70e6405ef0579eb4a776
fv1 comparison 67c0561509680cdee3ecead830215c93cd4530bc31a39ed0604a387afb53fde7
```

`compileall` completed successfully and `git diff --check` reported no whitespace errors for the Task 7 files.

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
- `results/source/Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx`
- `quality/prompt-suite.csv`
- `quality/scores.csv`
- `quality/outputs-index.csv`
- `failures/failure-register.csv`
- `evidence/evidence-index.csv`
- `evidence/source-locations.csv`
- `evidence/claim-evidence-map.csv`
- `evidence/manifest-sha256.txt`
- `reproduction/README.md`
- `validation/coverage-validation.json`
- `validation/data-validation.json`
- `validation/integrity-validation.json`

## Self-review

- Final attempt authority never leaks from fv1: status, execution, failure stage, and reason are constructed from fv2 only.
- Observations never leak into failed or blocked cases; exact case sets are checked both during construction and in source-rebuilt validation.
- Every measurement/quality record is linked to same-case fv1 raw evidence with the correct evidence role. Every failure is linked to the same-case final fv2 outcome and a missing-model attempt manifest.
- Stable IDs, all published fields, ordered three-repetition summary lineage, aggregation rules, literal values, prompt identities, output hashes, availability, and model-artifact rows are covered by mutation tests.
- Source paths are repository-relative and every published evidence record is hashed. Absolute model paths remain source-only and are not exported as portable artifact locations.
- The original fv1 workbook remains outside the route and is referenced by its repository-relative path and hash only.
- Existing unrelated recovered changes and source evidence were not staged or modified.

## Concerns

- The shared digest-derived evidence-ID contract represents the three byte-identical `source-models.json` copies as one content entity. One verified source location is published; the two redundant path aliases are not separate `EvidenceRecord` rows. All eight distinct final/intermediate attempt manifests and all three distinct attempt summaries are retained.
- Two fv1 preflight input paths are dangling symlinks in the recovered evidence tree. The complete preflight receipt that embeds their commands, paths, and hashes is indexed and hashed; the dangling targets are not claimed as standalone evidence. Mutation fixtures exclude only those unused dangling links.
- Task 8 will add report/workbook artifacts. It must deliberately regenerate the currently exact 22-entry route checksum manifest after the planned output set is complete; the non-destructive API is expected to reject a changed set during routine reruns.
