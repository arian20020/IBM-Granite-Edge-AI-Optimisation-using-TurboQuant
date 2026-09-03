# Final Results: Beginner Directory and File Guide

> This guide explains every directory and file in `docs/testing/final-results` without changing that checksum-validated release package.

## Start here

The final-results package contains the published evidence from five inference routes and one guarded comparison package. It accounts for 169 intended configurations: 81 passed, 6 failed, 28 were blocked and 54 were unavailable because validated model artefacts were missing.

For a readable technical overview, open the [cross-route Markdown report](final-results/06-cross-route-comparison/reports/cross-route-comparison-report.md). For an editable copy, use its DOCX file. For printing, use its PDF file. If you need rows for analysis, use the files in [`final-results/catalog/`](final-results/catalog/).

## Important words

- **Route:** one repository and runtime path used to run a group of tests.
- **Attempt:** one intended configuration with a final status.
- **Passed:** the route's required execution and evidence checks passed. This does not prove production, clinical or educational suitability.
- **Failed:** an attempted operation did not meet its required outcome.
- **Blocked:** the test was deliberately stopped by a safety, policy or preflight rule.
- **Artefact unavailable:** the required validated model file did not exist, so inference was not run.
- **Canonical:** the version selected as the official machine-readable record for this release.
- **Validation receipt:** a file recording which checks passed and which limitations remain.
- **SHA-256 checksum:** a fingerprint used to detect a missing or changed file.

## Where to look

| If you need... | Go to... |
| --- | --- |
| The fastest overall explanation | [`final-results/06-cross-route-comparison/reports/`](final-results/06-cross-route-comparison/reports/) |
| One route's readable results | That numbered route's `reports/` folder |
| Values for Excel or code | That route's `data/` folder or the shared `catalog/` folder |
| Why a result failed or did not run | That route's `data/failures.csv` and `evidence/` folder |
| Prompts and scoring rules | That route's `reproduction/quality/` folder |
| Hardware and software identity | That route's `reproduction/system/` folder |
| Proof that a package was checked | That route's `validation/` folder |
| Rules for comparing routes | [`final-results/06-cross-route-comparison/`](final-results/06-cross-route-comparison/) |
| Shared formats and schemas | [`final-results/standards/`](final-results/standards/) |

## Comparison and editing boundaries

- Do not treat blocked or unavailable rows as zero-valued measurements.
- Do not rank routes unless the comparability matrix says the required dimensions match.
- Only 15 matched experimental/official OpenVINO configurations are directly comparable for performance.
- Quality scores from different protocols are not automatically comparable.
- Do not edit files inside `final-results`. The release manifest validates their exact paths and bytes.
- A report summarises evidence. Use its linked data and validation receipts when auditing a claim.

## Complete directory and file reference

The sections below describe each folder and every file directly inside it. A file appears once, under its immediate parent folder.

## `final-results/`

Contains the curated, validated result packages used for final reporting.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`01-upstream-llama-cpp/`](final-results/01-upstream-llama-cpp/) | This folder covers the upstream llama.cpp baseline route. It is a curated publication package, not a live working directory. |
| [`02-atomicbot-turboquant/`](final-results/02-atomicbot-turboquant/) | This folder covers the AtomicBot TurboQuant route. It is a curated publication package, not a live working directory. |
| [`03-animehacker-tq3-0/`](final-results/03-animehacker-tq3-0/) | This folder covers the animehacker TQ3_0 route. It is a curated publication package, not a live working directory. |
| [`04-openvino-experimental-fork/`](final-results/04-openvino-experimental-fork/) | This folder covers the experimental OpenVINO fork route. It is a curated publication package, not a live working directory. |
| [`05-openvino-official-upstream/`](final-results/05-openvino-official-upstream/) | This folder covers the official upstream OpenVINO route. It is a curated publication package, not a live working directory. |
| [`06-cross-route-comparison/`](final-results/06-cross-route-comparison/) | This folder covers the guarded cross-route comparison package. It is a curated publication package, not a live working directory. |
| [`catalog/`](final-results/catalog/) | Combines route-level records into collection-wide tables. |
| [`standards/`](final-results/standards/) | Defines shared formats and rules used by every final-results package. |
| [`validation/`](final-results/validation/) | Contains checks showing whether the package structure and claims satisfy the release rules. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`CHANGELOG.md`](final-results/CHANGELOG.md) | History of released package changes. | Supporting repository file |
| [`LICENSES.md`](final-results/LICENSES.md) | Licensing, attribution and known licensing-gap information. | Supporting repository file |
| [`manifest-sha256.txt`](final-results/manifest-sha256.txt) | SHA-256 checksums used to detect missing or changed packaged files. | Supporting repository file |
| [`README.md`](final-results/README.md) | Readable Markdown document titled “Granite + TurboQuant unified testing results”. | Supporting repository file |
| [`REPRODUCING.md`](final-results/REPRODUCING.md) | Step-by-step guide to validating or rebuilding the published package. | Supporting repository file |
| [`ro-crate-metadata.json`](final-results/ro-crate-metadata.json) | RO-Crate metadata connecting files, sources and creation activities. | Supporting repository file |

### `final-results/01-upstream-llama-cpp/`

This folder covers the upstream llama.cpp baseline route. It is a curated publication package, not a live working directory.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`data/`](final-results/01-upstream-llama-cpp/data/) | Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the upstream llama.cpp baseline route. |
| [`evidence/`](final-results/01-upstream-llama-cpp/evidence/) | Connects published claims to the source material and checksums that support them. Here it applies to the upstream llama.cpp baseline route. |
| [`reports/`](final-results/01-upstream-llama-cpp/reports/) | Contains human-readable result reports and editable or printable versions. Here it applies to the upstream llama.cpp baseline route. |
| [`reproduction/`](final-results/01-upstream-llama-cpp/reproduction/) | Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the upstream llama.cpp baseline route. |
| [`validation/`](final-results/01-upstream-llama-cpp/validation/) | Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the upstream llama.cpp baseline route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/01-upstream-llama-cpp/README.md) | Readable Markdown document titled “Upstream llama.cpp final results”. | Supporting repository file |

#### `final-results/01-upstream-llama-cpp/data/`

Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`attempts.csv`](final-results/01-upstream-llama-cpp/data/attempts.csv) | One row per attempted configuration, including its final status. | Canonical published data |
| [`availability-matrix.csv`](final-results/01-upstream-llama-cpp/data/availability-matrix.csv) | CSV table with 13 data row(s). Main columns are `test_case_id`, `model_id`, `weight_format_id`, `cache_format_id`, `backend_id`, `status`. | Canonical published data |
| [`deviations.csv`](final-results/01-upstream-llama-cpp/data/deviations.csv) | CSV table with 12 data row(s). Main columns are `deviation_id`, `source_failure_id`, `scope_type`, `scope_test_ids`, `nonterminal`, `code`, `description` and 2 more. | Canonical published data |
| [`failures.csv`](final-results/01-upstream-llama-cpp/data/failures.csv) | Structured failed, blocked or unavailable outcomes and their evidence references. | Canonical published data |
| [`measurements.csv`](final-results/01-upstream-llama-cpp/data/measurements.csv) | Individual measured observations before route-level summarisation. | Canonical published data |
| [`quality.csv`](final-results/01-upstream-llama-cpp/data/quality.csv) | Detailed quality scores at the prompt or criterion level. | Canonical published data |
| [`route.json`](final-results/01-upstream-llama-cpp/data/route.json) | Machine-readable identity and scope for this route package. | Canonical published data |
| [`summaries.csv`](final-results/01-upstream-llama-cpp/data/summaries.csv) | CSV table with 65 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `summary_id`, `metric_name`, `value`, `unit` and 2 more. | Canonical published data |

#### `final-results/01-upstream-llama-cpp/evidence/`

Connects published claims to the source material and checksums that support them. Here it applies to the upstream llama.cpp baseline route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`failures/`](final-results/01-upstream-llama-cpp/evidence/failures/) | Preserves failed or blocked outcomes so they are not hidden from the final record. Here it applies to the upstream llama.cpp baseline route. |
| [`source/`](final-results/01-upstream-llama-cpp/evidence/source/) | Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the upstream llama.cpp baseline route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`claim-evidence-map.csv`](final-results/01-upstream-llama-cpp/evidence/claim-evidence-map.csv) | Maps each published claim to the evidence that supports or limits it. | Supporting repository file |
| [`evidence-index.csv`](final-results/01-upstream-llama-cpp/evidence/evidence-index.csv) | Lists evidence files and their provenance or checksum details. | Supporting repository file |
| [`manifest-sha256.txt`](final-results/01-upstream-llama-cpp/evidence/manifest-sha256.txt) | SHA-256 checksums used to detect missing or changed packaged files. | Supporting repository file |
| [`source-locations.csv`](final-results/01-upstream-llama-cpp/evidence/source-locations.csv) | CSV table with 656 data row(s). Main columns are `evidence_id`, `relative_path`, `source_or_derived`. | Supporting repository file |

##### `final-results/01-upstream-llama-cpp/evidence/failures/`

Preserves failed or blocked outcomes so they are not hidden from the final record. Here it applies to the upstream llama.cpp baseline route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`curated-logs/`](final-results/01-upstream-llama-cpp/evidence/failures/curated-logs/) | Contains the small, relevant log excerpts selected to explain a failure. Here it applies to the upstream llama.cpp baseline route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/01-upstream-llama-cpp/evidence/failures/README.md) | Readable Markdown document titled “Failures and deviations”. | Supporting repository file |

###### `final-results/01-upstream-llama-cpp/evidence/failures/curated-logs/`

Contains the small, relevant log excerpts selected to explain a failure. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/01-upstream-llama-cpp/evidence/failures/curated-logs/README.md) | Readable Markdown document titled “Curated log handling”. | Supporting repository file |

##### `final-results/01-upstream-llama-cpp/evidence/source/`

Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/01-upstream-llama-cpp/evidence/source/README.md) | Readable Markdown document titled “Source-result handling”. | Preserved evidence; do not edit |

#### `final-results/01-upstream-llama-cpp/reports/`

Contains human-readable result reports and editable or printable versions. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`upstream-llama-cpp-report.docx`](final-results/01-upstream-llama-cpp/reports/upstream-llama-cpp-report.docx) | Editable Word version of upstream llama cpp report for review or handoff. | Generated or review artefact |
| [`upstream-llama-cpp-report.md`](final-results/01-upstream-llama-cpp/reports/upstream-llama-cpp-report.md) | Readable Markdown document titled “Upstream llama.cpp Final Test Report”. | Supporting repository file |
| [`upstream-llama-cpp-report.pdf`](final-results/01-upstream-llama-cpp/reports/upstream-llama-cpp-report.pdf) | Printable PDF version of upstream llama cpp report. Use its source file when edits are needed. | Generated or review artefact |

#### `final-results/01-upstream-llama-cpp/reproduction/`

Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the upstream llama.cpp baseline route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`protocol/`](final-results/01-upstream-llama-cpp/reproduction/protocol/) | Defines what was meant to be tested, in what order, and under which rules. Here it applies to the upstream llama.cpp baseline route. |
| [`quality/`](final-results/01-upstream-llama-cpp/reproduction/quality/) | Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the upstream llama.cpp baseline route. |
| [`scripts/`](final-results/01-upstream-llama-cpp/reproduction/scripts/) | Contains route-specific experiment scripts and compatibility entry points. Here it applies to the upstream llama.cpp baseline route. |
| [`system/`](final-results/01-upstream-llama-cpp/reproduction/system/) | Records the hardware, software and repository identity of the test environment. Here it applies to the upstream llama.cpp baseline route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`commands.md`](final-results/01-upstream-llama-cpp/reproduction/commands.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |
| [`dependencies.md`](final-results/01-upstream-llama-cpp/reproduction/dependencies.md) | Readable Markdown document titled “Validation dependencies”. | Supporting repository file |
| [`README.md`](final-results/01-upstream-llama-cpp/reproduction/README.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |

##### `final-results/01-upstream-llama-cpp/reproduction/protocol/`

Defines what was meant to be tested, in what order, and under which rules. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`execution-sequence.md`](final-results/01-upstream-llama-cpp/reproduction/protocol/execution-sequence.md) | Readable Markdown document titled “Execution sequence”. | Controlled test input |
| [`intended-test-matrix.csv`](final-results/01-upstream-llama-cpp/reproduction/protocol/intended-test-matrix.csv) | CSV table with 13 data row(s). Main columns are `ID`, `Model`, `Weights`, `K/V cache`, `Device`, `Context`, `Purpose` and 1 more. | Controlled test input |
| [`metric-definitions.md`](final-results/01-upstream-llama-cpp/reproduction/protocol/metric-definitions.md) | Readable Markdown document titled “Metric definitions”. | Controlled test input |
| [`test-plan.md`](final-results/01-upstream-llama-cpp/reproduction/protocol/test-plan.md) | Readable Markdown document titled “Test plan”. | Controlled test input |

##### `final-results/01-upstream-llama-cpp/reproduction/quality/`

Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`adjudication-log.csv`](final-results/01-upstream-llama-cpp/reproduction/quality/adjudication-log.csv) | CSV table with 1 data row(s). Main columns are `adjudication_id`, `method`, `status`. | Supporting repository file |
| [`calibration.md`](final-results/01-upstream-llama-cpp/reproduction/quality/calibration.md) | Readable Markdown document titled “Calibration”. | Supporting repository file |
| [`outputs-index.csv`](final-results/01-upstream-llama-cpp/reproduction/quality/outputs-index.csv) | CSV table with 54 data row(s). Main columns are `test_case_id`, `prompt_id`, `source_evidence_id`. | Supporting repository file |
| [`prompt-suite.csv`](final-results/01-upstream-llama-cpp/reproduction/quality/prompt-suite.csv) | CSV table with 6 data row(s). Main columns are `prompt_id`, `task_type`, `scope`, `deterministic_checks_json`, `generation_settings_json`, `prompt_set_id`. | Supporting repository file |
| [`README.md`](final-results/01-upstream-llama-cpp/reproduction/quality/README.md) | Readable Markdown document titled “Quality evidence”. | Supporting repository file |
| [`rubric.md`](final-results/01-upstream-llama-cpp/reproduction/quality/rubric.md) | Readable Markdown document titled “GTQ-QUALITY-RUBRIC-v1”. | Supporting repository file |

##### `final-results/01-upstream-llama-cpp/reproduction/scripts/`

Contains route-specific experiment scripts and compatibility entry points. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/01-upstream-llama-cpp/reproduction/scripts/README.md) | Readable Markdown document titled “Maintained implementation”. | Supporting repository file |

##### `final-results/01-upstream-llama-cpp/reproduction/system/`

Records the hardware, software and repository identity of the test environment. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`environment.txt`](final-results/01-upstream-llama-cpp/reproduction/system/environment.txt) | Plain-text evidence or diagnostic output for environment; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`hardware.json`](final-results/01-upstream-llama-cpp/reproduction/system/hardware.json) | Stores a JSON object with top-level fields `cpu`, `gpu`, `gpu_dedicated_memory`, `machine_id`, `npu`, `ram_bytes`. | Supporting repository file |
| [`model-artifacts.csv`](final-results/01-upstream-llama-cpp/reproduction/system/model-artifacts.csv) | CSV table with 6 data row(s). Main columns are `model_id`, `weight_format_id`, `status`. | Supporting repository file |
| [`repository.json`](final-results/01-upstream-llama-cpp/reproduction/system/repository.json) | Stores a JSON object with top-level fields `best_observed_cpu_test_id`, `best_observed_intel_gpu_test_id`, `commit`, `deviation_rows`, `historical_missing_metric_display`, `performance_register_reason`, `performance_register_status`, `raw_results_reason`, …. | Supporting repository file |
| [`software.json`](final-results/01-upstream-llama-cpp/reproduction/system/software.json) | Stores a JSON object with top-level fields `cmake`, `compiler`, `os`, `performance_register_rows`, `sycl`, `vulkan_sdk`. | Supporting repository file |

#### `final-results/01-upstream-llama-cpp/validation/`

Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the upstream llama.cpp baseline route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`validation.json`](final-results/01-upstream-llama-cpp/validation/validation.json) | Machine-readable validation result for tools and automated checks. | Validation receipt |
| [`validation.md`](final-results/01-upstream-llama-cpp/validation/validation.md) | Readable explanation of the validation result and remaining limitations. | Validation receipt |

### `final-results/02-atomicbot-turboquant/`

This folder covers the AtomicBot TurboQuant route. It is a curated publication package, not a live working directory.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`data/`](final-results/02-atomicbot-turboquant/data/) | Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the AtomicBot TurboQuant route. |
| [`evidence/`](final-results/02-atomicbot-turboquant/evidence/) | Connects published claims to the source material and checksums that support them. Here it applies to the AtomicBot TurboQuant route. |
| [`reports/`](final-results/02-atomicbot-turboquant/reports/) | Contains human-readable result reports and editable or printable versions. Here it applies to the AtomicBot TurboQuant route. |
| [`reproduction/`](final-results/02-atomicbot-turboquant/reproduction/) | Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the AtomicBot TurboQuant route. |
| [`validation/`](final-results/02-atomicbot-turboquant/validation/) | Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the AtomicBot TurboQuant route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/02-atomicbot-turboquant/README.md) | Readable Markdown document titled “AtomicBot TurboQuant final results”. | Supporting repository file |

#### `final-results/02-atomicbot-turboquant/data/`

Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`attempts.csv`](final-results/02-atomicbot-turboquant/data/attempts.csv) | One row per attempted configuration, including its final status. | Canonical published data |
| [`availability-matrix.csv`](final-results/02-atomicbot-turboquant/data/availability-matrix.csv) | CSV table with 19 data row(s). Main columns are `test_case_id`, `model_id`, `weight_format_id`, `cache_format_id`, `backend_id`, `status`. | Canonical published data |
| [`deviations.csv`](final-results/02-atomicbot-turboquant/data/deviations.csv) | CSV table with 5 data row(s). Main columns are `deviation_id`, `scope_type`, `scope_ids_json`, `nonterminal`, `code`, `status`, `reason` and 1 more. | Canonical published data |
| [`failures.csv`](final-results/02-atomicbot-turboquant/data/failures.csv) | Structured failed, blocked or unavailable outcomes and their evidence references. | Canonical published data |
| [`measurements.csv`](final-results/02-atomicbot-turboquant/data/measurements.csv) | Individual measured observations before route-level summarisation. | Canonical published data |
| [`quality.csv`](final-results/02-atomicbot-turboquant/data/quality.csv) | Detailed quality scores at the prompt or criterion level. | Canonical published data |
| [`route.json`](final-results/02-atomicbot-turboquant/data/route.json) | Machine-readable identity and scope for this route package. | Canonical published data |
| [`summaries.csv`](final-results/02-atomicbot-turboquant/data/summaries.csv) | CSV table with 114 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `summary_id`, `metric_name`, `value`, `unit` and 2 more. | Canonical published data |

#### `final-results/02-atomicbot-turboquant/evidence/`

Connects published claims to the source material and checksums that support them. Here it applies to the AtomicBot TurboQuant route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`failures/`](final-results/02-atomicbot-turboquant/evidence/failures/) | Preserves failed or blocked outcomes so they are not hidden from the final record. Here it applies to the AtomicBot TurboQuant route. |
| [`source/`](final-results/02-atomicbot-turboquant/evidence/source/) | Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the AtomicBot TurboQuant route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`claim-evidence-map.csv`](final-results/02-atomicbot-turboquant/evidence/claim-evidence-map.csv) | Maps each published claim to the evidence that supports or limits it. | Supporting repository file |
| [`evidence-index.csv`](final-results/02-atomicbot-turboquant/evidence/evidence-index.csv) | Lists evidence files and their provenance or checksum details. | Supporting repository file |
| [`manifest-sha256.txt`](final-results/02-atomicbot-turboquant/evidence/manifest-sha256.txt) | SHA-256 checksums used to detect missing or changed packaged files. | Supporting repository file |
| [`source-locations.csv`](final-results/02-atomicbot-turboquant/evidence/source-locations.csv) | CSV table with 286 data row(s). Main columns are `evidence_id`, `relative_path`, `source_or_derived`. | Supporting repository file |

##### `final-results/02-atomicbot-turboquant/evidence/failures/`

Preserves failed or blocked outcomes so they are not hidden from the final record. Here it applies to the AtomicBot TurboQuant route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`curated-logs/`](final-results/02-atomicbot-turboquant/evidence/failures/curated-logs/) | Contains the small, relevant log excerpts selected to explain a failure. Here it applies to the AtomicBot TurboQuant route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/02-atomicbot-turboquant/evidence/failures/README.md) | Readable Markdown document titled “Failures and deviations”. | Supporting repository file |

###### `final-results/02-atomicbot-turboquant/evidence/failures/curated-logs/`

Contains the small, relevant log excerpts selected to explain a failure. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/02-atomicbot-turboquant/evidence/failures/curated-logs/README.md) | Readable Markdown document titled “Curated log handling”. | Supporting repository file |

##### `final-results/02-atomicbot-turboquant/evidence/source/`

Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/02-atomicbot-turboquant/evidence/source/README.md) | Readable Markdown document titled “Source-result handling”. | Preserved evidence; do not edit |

#### `final-results/02-atomicbot-turboquant/reports/`

Contains human-readable result reports and editable or printable versions. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`atomicbot-turboquant-report.docx`](final-results/02-atomicbot-turboquant/reports/atomicbot-turboquant-report.docx) | Editable Word version of atomicbot turboquant report for review or handoff. | Generated or review artefact |
| [`atomicbot-turboquant-report.md`](final-results/02-atomicbot-turboquant/reports/atomicbot-turboquant-report.md) | Readable Markdown document titled “AtomicBot TurboQuant Final Test Report”. | Supporting repository file |
| [`atomicbot-turboquant-report.pdf`](final-results/02-atomicbot-turboquant/reports/atomicbot-turboquant-report.pdf) | Printable PDF version of atomicbot turboquant report. Use its source file when edits are needed. | Generated or review artefact |

#### `final-results/02-atomicbot-turboquant/reproduction/`

Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the AtomicBot TurboQuant route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`protocol/`](final-results/02-atomicbot-turboquant/reproduction/protocol/) | Defines what was meant to be tested, in what order, and under which rules. Here it applies to the AtomicBot TurboQuant route. |
| [`quality/`](final-results/02-atomicbot-turboquant/reproduction/quality/) | Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the AtomicBot TurboQuant route. |
| [`scripts/`](final-results/02-atomicbot-turboquant/reproduction/scripts/) | Contains route-specific experiment scripts and compatibility entry points. Here it applies to the AtomicBot TurboQuant route. |
| [`system/`](final-results/02-atomicbot-turboquant/reproduction/system/) | Records the hardware, software and repository identity of the test environment. Here it applies to the AtomicBot TurboQuant route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`commands.md`](final-results/02-atomicbot-turboquant/reproduction/commands.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |
| [`dependencies.md`](final-results/02-atomicbot-turboquant/reproduction/dependencies.md) | Readable Markdown document titled “Validation dependencies”. | Supporting repository file |
| [`README.md`](final-results/02-atomicbot-turboquant/reproduction/README.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |

##### `final-results/02-atomicbot-turboquant/reproduction/protocol/`

Defines what was meant to be tested, in what order, and under which rules. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`execution-sequence.md`](final-results/02-atomicbot-turboquant/reproduction/protocol/execution-sequence.md) | Readable Markdown document titled “Execution sequence”. | Controlled test input |
| [`intended-test-matrix.csv`](final-results/02-atomicbot-turboquant/reproduction/protocol/intended-test-matrix.csv) | CSV table with 19 data row(s). Main columns are `ID`, `Model`, `KV cache`, `Execution`, `Context`, `Purpose / limitation`, `Status`. | Controlled test input |
| [`metric-definitions.md`](final-results/02-atomicbot-turboquant/reproduction/protocol/metric-definitions.md) | Readable Markdown document titled “Metric definitions”. | Controlled test input |
| [`test-plan.md`](final-results/02-atomicbot-turboquant/reproduction/protocol/test-plan.md) | Readable Markdown document titled “Test plan”. | Controlled test input |

##### `final-results/02-atomicbot-turboquant/reproduction/quality/`

Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`adjudication-log.csv`](final-results/02-atomicbot-turboquant/reproduction/quality/adjudication-log.csv) | CSV table with 114 data row(s). Main columns are `quality_id`, `test_case_id`, `prompt_id`, `adjudication_key`, `dimensions_json`, `weighted_score`, `critical_caps_json` and 5 more. | Supporting repository file |
| [`calibration.md`](final-results/02-atomicbot-turboquant/reproduction/quality/calibration.md) | Readable Markdown document titled “Calibration”. | Supporting repository file |
| [`outputs-index.csv`](final-results/02-atomicbot-turboquant/reproduction/quality/outputs-index.csv) | CSV table with 114 data row(s). Main columns are `test_case_id`, `prompt_id`, `output_sha256`, `adjudication_key`, `source_evidence_id`. | Supporting repository file |
| [`prompt-suite.csv`](final-results/02-atomicbot-turboquant/reproduction/quality/prompt-suite.csv) | CSV table with 6 data row(s). Main columns are `prompt_id`, `task`, `deterministic_checks_json`, `generation_settings_json`, `scope`, `comparability`. | Supporting repository file |
| [`README.md`](final-results/02-atomicbot-turboquant/reproduction/quality/README.md) | Readable Markdown document titled “Quality evidence”. | Supporting repository file |
| [`rubric.md`](final-results/02-atomicbot-turboquant/reproduction/quality/rubric.md) | Readable Markdown document titled “GTQ-QUALITY-RUBRIC-v1 — limited/provisional application”. | Supporting repository file |

##### `final-results/02-atomicbot-turboquant/reproduction/scripts/`

Contains route-specific experiment scripts and compatibility entry points. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/02-atomicbot-turboquant/reproduction/scripts/README.md) | Readable Markdown document titled “Maintained implementation”. | Supporting repository file |

##### `final-results/02-atomicbot-turboquant/reproduction/system/`

Records the hardware, software and repository identity of the test environment. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`environment.txt`](final-results/02-atomicbot-turboquant/reproduction/system/environment.txt) | Plain-text evidence or diagnostic output for environment; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`hardware.json`](final-results/02-atomicbot-turboquant/reproduction/system/hardware.json) | Stores a JSON object with top-level fields `available_memory_mib`, `logical_cpus`, `machine`, `npu`, `platform`, `processor`, `vulkan_sdk_version`. | Supporting repository file |
| [`model-artifacts.csv`](final-results/02-atomicbot-turboquant/reproduction/system/model-artifacts.csv) | CSV table with 19 data row(s). Main columns are `model_id`, `weight_format_id`, `status`. | Supporting repository file |
| [`repository.json`](final-results/02-atomicbot-turboquant/reproduction/system/repository.json) | Stores a JSON object with top-level fields `branch`, `commit`, `deviation_rows`, `measurement_field_evidence`, `quality_adjudications`, `quality_authority`, `quality_calibration`, `quality_contract`, …. | Supporting repository file |
| [`software.json`](final-results/02-atomicbot-turboquant/reproduction/system/software.json) | Stores a JSON object with top-level fields `reporting_interpreter`, `tool_versions`. | Supporting repository file |

#### `final-results/02-atomicbot-turboquant/validation/`

Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the AtomicBot TurboQuant route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`validation.json`](final-results/02-atomicbot-turboquant/validation/validation.json) | Machine-readable validation result for tools and automated checks. | Validation receipt |
| [`validation.md`](final-results/02-atomicbot-turboquant/validation/validation.md) | Readable explanation of the validation result and remaining limitations. | Validation receipt |

### `final-results/03-animehacker-tq3-0/`

This folder covers the animehacker TQ3_0 route. It is a curated publication package, not a live working directory.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`data/`](final-results/03-animehacker-tq3-0/data/) | Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the animehacker TQ3_0 route. |
| [`evidence/`](final-results/03-animehacker-tq3-0/evidence/) | Connects published claims to the source material and checksums that support them. Here it applies to the animehacker TQ3_0 route. |
| [`reports/`](final-results/03-animehacker-tq3-0/reports/) | Contains human-readable result reports and editable or printable versions. Here it applies to the animehacker TQ3_0 route. |
| [`reproduction/`](final-results/03-animehacker-tq3-0/reproduction/) | Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the animehacker TQ3_0 route. |
| [`validation/`](final-results/03-animehacker-tq3-0/validation/) | Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the animehacker TQ3_0 route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/03-animehacker-tq3-0/README.md) | Readable Markdown document titled “animehacker TQ3_0 final results”. | Supporting repository file |

#### `final-results/03-animehacker-tq3-0/data/`

Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`attempts.csv`](final-results/03-animehacker-tq3-0/data/attempts.csv) | One row per attempted configuration, including its final status. | Canonical published data |
| [`availability-matrix.csv`](final-results/03-animehacker-tq3-0/data/availability-matrix.csv) | CSV table with 10 data row(s). Main columns are `test_case_id`, `model_id`, `weight_format_id`, `cache_format_id`, `backend_id`, `status`. | Canonical published data |
| [`deviations.csv`](final-results/03-animehacker-tq3-0/data/deviations.csv) | CSV table with 11 data row(s). Main columns are `deviation_id`, `scope_ids`, `code`, `description`, `resolution`, `terminal`. | Canonical published data |
| [`failures.csv`](final-results/03-animehacker-tq3-0/data/failures.csv) | Structured failed, blocked or unavailable outcomes and their evidence references. | Canonical published data |
| [`measurements.csv`](final-results/03-animehacker-tq3-0/data/measurements.csv) | Individual measured observations before route-level summarisation. | Canonical published data |
| [`quality.csv`](final-results/03-animehacker-tq3-0/data/quality.csv) | Detailed quality scores at the prompt or criterion level. | Canonical published data |
| [`resource-observations.csv`](final-results/03-animehacker-tq3-0/data/resource-observations.csv) | CSV table with 10 data row(s). Main columns are `test_case_id`, `inclusion_status`, `summary_evidence_id`, `time_to_first_token_ms`, `prompt_tokens_per_second`, `generation_tokens_per_second`, `peak_working_set_mb` and 5 more. | Canonical published data |
| [`route.json`](final-results/03-animehacker-tq3-0/data/route.json) | Machine-readable identity and scope for this route package. | Canonical published data |
| [`summaries.csv`](final-results/03-animehacker-tq3-0/data/summaries.csv) | CSV table with 35 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `summary_id`, `metric_name`, `value`, `unit` and 2 more. | Canonical published data |

#### `final-results/03-animehacker-tq3-0/evidence/`

Connects published claims to the source material and checksums that support them. Here it applies to the animehacker TQ3_0 route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`failures/`](final-results/03-animehacker-tq3-0/evidence/failures/) | Preserves failed or blocked outcomes so they are not hidden from the final record. Here it applies to the animehacker TQ3_0 route. |
| [`source/`](final-results/03-animehacker-tq3-0/evidence/source/) | Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the animehacker TQ3_0 route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`claim-evidence-map.csv`](final-results/03-animehacker-tq3-0/evidence/claim-evidence-map.csv) | Maps each published claim to the evidence that supports or limits it. | Supporting repository file |
| [`evidence-index.csv`](final-results/03-animehacker-tq3-0/evidence/evidence-index.csv) | Lists evidence files and their provenance or checksum details. | Supporting repository file |
| [`manifest-sha256.txt`](final-results/03-animehacker-tq3-0/evidence/manifest-sha256.txt) | SHA-256 checksums used to detect missing or changed packaged files. | Supporting repository file |
| [`source-locations.csv`](final-results/03-animehacker-tq3-0/evidence/source-locations.csv) | CSV table with 742 data row(s). Main columns are `evidence_id`, `relative_path`, `source_or_derived`. | Supporting repository file |

##### `final-results/03-animehacker-tq3-0/evidence/failures/`

Preserves failed or blocked outcomes so they are not hidden from the final record. Here it applies to the animehacker TQ3_0 route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`curated-logs/`](final-results/03-animehacker-tq3-0/evidence/failures/curated-logs/) | Contains the small, relevant log excerpts selected to explain a failure. Here it applies to the animehacker TQ3_0 route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/03-animehacker-tq3-0/evidence/failures/README.md) | Readable Markdown document titled “Failures and deviations”. | Supporting repository file |

###### `final-results/03-animehacker-tq3-0/evidence/failures/curated-logs/`

Contains the small, relevant log excerpts selected to explain a failure. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/03-animehacker-tq3-0/evidence/failures/curated-logs/README.md) | Readable Markdown document titled “Curated log handling”. | Supporting repository file |

##### `final-results/03-animehacker-tq3-0/evidence/source/`

Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/03-animehacker-tq3-0/evidence/source/README.md) | Readable Markdown document titled “Source-result handling”. | Preserved evidence; do not edit |

#### `final-results/03-animehacker-tq3-0/reports/`

Contains human-readable result reports and editable or printable versions. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`animehacker-tq3-0-report.docx`](final-results/03-animehacker-tq3-0/reports/animehacker-tq3-0-report.docx) | Editable Word version of animehacker tq3 0 report for review or handoff. | Generated or review artefact |
| [`animehacker-tq3-0-report.md`](final-results/03-animehacker-tq3-0/reports/animehacker-tq3-0-report.md) | Readable Markdown document titled “animehacker TQ3_0 Final Test Report”. | Supporting repository file |
| [`animehacker-tq3-0-report.pdf`](final-results/03-animehacker-tq3-0/reports/animehacker-tq3-0-report.pdf) | Printable PDF version of animehacker tq3 0 report. Use its source file when edits are needed. | Generated or review artefact |

#### `final-results/03-animehacker-tq3-0/reproduction/`

Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the animehacker TQ3_0 route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`protocol/`](final-results/03-animehacker-tq3-0/reproduction/protocol/) | Defines what was meant to be tested, in what order, and under which rules. Here it applies to the animehacker TQ3_0 route. |
| [`quality/`](final-results/03-animehacker-tq3-0/reproduction/quality/) | Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the animehacker TQ3_0 route. |
| [`scripts/`](final-results/03-animehacker-tq3-0/reproduction/scripts/) | Contains route-specific experiment scripts and compatibility entry points. Here it applies to the animehacker TQ3_0 route. |
| [`system/`](final-results/03-animehacker-tq3-0/reproduction/system/) | Records the hardware, software and repository identity of the test environment. Here it applies to the animehacker TQ3_0 route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`commands.md`](final-results/03-animehacker-tq3-0/reproduction/commands.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |
| [`dependencies.md`](final-results/03-animehacker-tq3-0/reproduction/dependencies.md) | Readable Markdown document titled “Validation dependencies”. | Supporting repository file |
| [`README.md`](final-results/03-animehacker-tq3-0/reproduction/README.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |

##### `final-results/03-animehacker-tq3-0/reproduction/protocol/`

Defines what was meant to be tested, in what order, and under which rules. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`execution-sequence.md`](final-results/03-animehacker-tq3-0/reproduction/protocol/execution-sequence.md) | Readable Markdown document titled “Execution sequence”. | Controlled test input |
| [`intended-test-matrix.csv`](final-results/03-animehacker-tq3-0/reproduction/protocol/intended-test-matrix.csv) | CSV table with 10 data row(s). Main columns are `test_id`, `phase`, `description`, `model_id`, `model_path_env`, `format`, `cache` and 10 more. | Controlled test input |
| [`metric-definitions.md`](final-results/03-animehacker-tq3-0/reproduction/protocol/metric-definitions.md) | Readable Markdown document titled “Metric definitions”. | Controlled test input |
| [`test-plan.md`](final-results/03-animehacker-tq3-0/reproduction/protocol/test-plan.md) | Readable Markdown document titled “Test plan”. | Controlled test input |

##### `final-results/03-animehacker-tq3-0/reproduction/quality/`

Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`adjudication-log.csv`](final-results/03-animehacker-tq3-0/reproduction/quality/adjudication-log.csv) | CSV table with 42 data row(s). Main columns are `test_case_id`, `prompt_id`, `score`, `deterministic_pass`, `dimensions_json`, `critical_caps_json`, `critical_cap_reason`. | Supporting repository file |
| [`calibration.md`](final-results/03-animehacker-tq3-0/reproduction/quality/calibration.md) | Readable Markdown document titled “Calibration”. | Supporting repository file |
| [`outputs-index.csv`](final-results/03-animehacker-tq3-0/reproduction/quality/outputs-index.csv) | CSV table with 42 data row(s). Main columns are `test_case_id`, `prompt_id`, `output_sha256`, `source_evidence_id`. | Supporting repository file |
| [`prompt-suite.csv`](final-results/03-animehacker-tq3-0/reproduction/quality/prompt-suite.csv) | CSV table with 6 data row(s). Main columns are `prompt_id`, `task`, `deterministic_checks_json`, `scope`, `comparability`. | Supporting repository file |
| [`README.md`](final-results/03-animehacker-tq3-0/reproduction/quality/README.md) | Readable Markdown document titled “Quality evidence”. | Supporting repository file |
| [`rubric.md`](final-results/03-animehacker-tq3-0/reproduction/quality/rubric.md) | Readable Markdown document titled “GTQ-QUALITY-RUBRIC-v1 historical application”. | Supporting repository file |

##### `final-results/03-animehacker-tq3-0/reproduction/scripts/`

Contains route-specific experiment scripts and compatibility entry points. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/03-animehacker-tq3-0/reproduction/scripts/README.md) | Readable Markdown document titled “Maintained implementation”. | Supporting repository file |

##### `final-results/03-animehacker-tq3-0/reproduction/system/`

Records the hardware, software and repository identity of the test environment. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`environment.txt`](final-results/03-animehacker-tq3-0/reproduction/system/environment.txt) | Plain-text evidence or diagnostic output for environment; read it as supporting detail, not as the final conclusion. | Supporting repository file |
| [`hardware.json`](final-results/03-animehacker-tq3-0/reproduction/system/hardware.json) | Stores a JSON object with top-level fields `graphics`, `machine_id`, `npu`, `operating_system`, `processor`, `ram`. | Supporting repository file |
| [`model-artifacts.csv`](final-results/03-animehacker-tq3-0/reproduction/system/model-artifacts.csv) | CSV table with 10 data row(s). Main columns are `model_id`, `weight_format_id`, `status`. | Supporting repository file |
| [`repository.json`](final-results/03-animehacker-tq3-0/reproduction/system/repository.json) | Stores a JSON object with top-level fields `branch`, `commit`, `formal_runtime_summary_count`, `historical_failure_attempt_count`, `historical_failure_entity_hashes`, `historical_failure_rows`, `intended_matrix_entities`, `matrix_entity_hashes`, …. | Supporting repository file |
| [`software.json`](final-results/03-animehacker-tq3-0/reproduction/system/software.json) | Stores a JSON object with top-level fields `cmake`, `cpu_tests`, `msvc`, `ninja`, `sycl_tests`, `vulkan_tq3`. | Supporting repository file |

#### `final-results/03-animehacker-tq3-0/validation/`

Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the animehacker TQ3_0 route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`validation.json`](final-results/03-animehacker-tq3-0/validation/validation.json) | Machine-readable validation result for tools and automated checks. | Validation receipt |
| [`validation.md`](final-results/03-animehacker-tq3-0/validation/validation.md) | Readable explanation of the validation result and remaining limitations. | Validation receipt |

### `final-results/04-openvino-experimental-fork/`

This folder covers the experimental OpenVINO fork route. It is a curated publication package, not a live working directory.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`data/`](final-results/04-openvino-experimental-fork/data/) | Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the experimental OpenVINO fork route. |
| [`evidence/`](final-results/04-openvino-experimental-fork/evidence/) | Connects published claims to the source material and checksums that support them. Here it applies to the experimental OpenVINO fork route. |
| [`reports/`](final-results/04-openvino-experimental-fork/reports/) | Contains human-readable result reports and editable or printable versions. Here it applies to the experimental OpenVINO fork route. |
| [`reproduction/`](final-results/04-openvino-experimental-fork/reproduction/) | Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the experimental OpenVINO fork route. |
| [`validation/`](final-results/04-openvino-experimental-fork/validation/) | Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the experimental OpenVINO fork route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/04-openvino-experimental-fork/README.md) | Readable Markdown document titled “Experimental OpenVINO fork final results”. | Supporting repository file |

#### `final-results/04-openvino-experimental-fork/data/`

Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the experimental OpenVINO fork route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`attempts.csv`](final-results/04-openvino-experimental-fork/data/attempts.csv) | One row per attempted configuration, including its final status. | Canonical published data |
| [`availability-matrix.csv`](final-results/04-openvino-experimental-fork/data/availability-matrix.csv) | CSV table with 81 data row(s). Main columns are `test_case_id`, `model_id`, `weight_format_id`, `cache_format_id`, `status`, `executed`, `reason`. | Canonical published data |
| [`failures.csv`](final-results/04-openvino-experimental-fork/data/failures.csv) | Structured failed, blocked or unavailable outcomes and their evidence references. | Canonical published data |
| [`measurements.csv`](final-results/04-openvino-experimental-fork/data/measurements.csv) | Individual measured observations before route-level summarisation. | Canonical published data |
| [`quality.csv`](final-results/04-openvino-experimental-fork/data/quality.csv) | Detailed quality scores at the prompt or criterion level. | Canonical published data |
| [`route.json`](final-results/04-openvino-experimental-fork/data/route.json) | Machine-readable identity and scope for this route package. | Canonical published data |
| [`summaries.csv`](final-results/04-openvino-experimental-fork/data/summaries.csv) | CSV table with 81 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `summary_id`, `metric_name`, `value`, `unit` and 2 more. | Canonical published data |

#### `final-results/04-openvino-experimental-fork/evidence/`

Connects published claims to the source material and checksums that support them. Here it applies to the experimental OpenVINO fork route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`source/`](final-results/04-openvino-experimental-fork/evidence/source/) | Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the experimental OpenVINO fork route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`claim-evidence-map.csv`](final-results/04-openvino-experimental-fork/evidence/claim-evidence-map.csv) | Maps each published claim to the evidence that supports or limits it. | Supporting repository file |
| [`evidence-index.csv`](final-results/04-openvino-experimental-fork/evidence/evidence-index.csv) | Lists evidence files and their provenance or checksum details. | Supporting repository file |
| [`manifest-sha256.txt`](final-results/04-openvino-experimental-fork/evidence/manifest-sha256.txt) | SHA-256 checksums used to detect missing or changed packaged files. | Supporting repository file |
| [`source-locations.csv`](final-results/04-openvino-experimental-fork/evidence/source-locations.csv) | CSV table with 81 data row(s). Main columns are `evidence_id`, `role`, `relative_path`, `sha256`, `size_bytes`, `source_label`. | Supporting repository file |

##### `final-results/04-openvino-experimental-fork/evidence/source/`

Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the experimental OpenVINO fork route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx`](final-results/04-openvino-experimental-fork/evidence/source/Granite_OpenVINO_Final_Healthcare_Education_Results_2026-08-30.xlsx) | Excel workbook containing Granite OpenVINO Final Healthcare Education Results 2026 08 30. It is preserved source evidence; use the portable copy for normal sharing. | Preserved evidence; do not edit |

#### `final-results/04-openvino-experimental-fork/reports/`

Contains human-readable result reports and editable or printable versions. Here it applies to the experimental OpenVINO fork route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`openvino-experimental-fork-report.docx`](final-results/04-openvino-experimental-fork/reports/openvino-experimental-fork-report.docx) | Editable Word version of openvino experimental fork report for review or handoff. | Generated or review artefact |
| [`openvino-experimental-fork-report.md`](final-results/04-openvino-experimental-fork/reports/openvino-experimental-fork-report.md) | Readable Markdown document titled “Experimental OpenVINO fork final results”. | Supporting repository file |
| [`openvino-experimental-fork-report.pdf`](final-results/04-openvino-experimental-fork/reports/openvino-experimental-fork-report.pdf) | Printable PDF version of openvino experimental fork report. Use its source file when edits are needed. | Generated or review artefact |
| [`openvino-experimental-fork-results-provenance.json`](final-results/04-openvino-experimental-fork/reports/openvino-experimental-fork-results-provenance.json) | Stores a JSON object with top-level fields `activity_id`, `cached_formula_value_count`, `formula_count`, `formulas_preserved`, `machine_absolute_path_count`, `openpyxl_version`, `output_path`, `output_role`, …. | Supporting repository file |
| [`openvino-experimental-fork-results.xlsx`](final-results/04-openvino-experimental-fork/reports/openvino-experimental-fork-results.xlsx) | Excel workbook containing openvino experimental fork results. | Generated or review artefact |

#### `final-results/04-openvino-experimental-fork/reproduction/`

Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the experimental OpenVINO fork route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`protocol/`](final-results/04-openvino-experimental-fork/reproduction/protocol/) | Defines what was meant to be tested, in what order, and under which rules. Here it applies to the experimental OpenVINO fork route. |
| [`quality/`](final-results/04-openvino-experimental-fork/reproduction/quality/) | Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the experimental OpenVINO fork route. |
| [`system/`](final-results/04-openvino-experimental-fork/reproduction/system/) | Records the hardware, software and repository identity of the test environment. Here it applies to the experimental OpenVINO fork route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/04-openvino-experimental-fork/reproduction/README.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |

##### `final-results/04-openvino-experimental-fork/reproduction/protocol/`

Defines what was meant to be tested, in what order, and under which rules. Here it applies to the experimental OpenVINO fork route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`intended-test-matrix.csv`](final-results/04-openvino-experimental-fork/reproduction/protocol/intended-test-matrix.csv) | CSV table with 81 data row(s). Main columns are `test_case_id`, `model_id`, `weight_format_id`, `cache_format_id`, `intended`. | Controlled test input |

##### `final-results/04-openvino-experimental-fork/reproduction/quality/`

Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the experimental OpenVINO fork route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`outputs-index.csv`](final-results/04-openvino-experimental-fork/reproduction/quality/outputs-index.csv) | CSV table with 1296 data row(s). Main columns are `output_id`, `test_case_id`, `prompt_id`, `domain`, `prompt_length`, `status`, `valid_output` and 4 more. | Supporting repository file |
| [`prompt-suite.csv`](final-results/04-openvino-experimental-fork/reproduction/quality/prompt-suite.csv) | CSV table with 48 data row(s). Main columns are `prompt_suite_id`, `prompt_id`, `domain`, `prompt_length`, `input_evidence_id`, `relative_path`, `sha256`. | Supporting repository file |

##### `final-results/04-openvino-experimental-fork/reproduction/system/`

Records the hardware, software and repository identity of the test environment. Here it applies to the experimental OpenVINO fork route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`hardware.json`](final-results/04-openvino-experimental-fork/reproduction/system/hardware.json) | Stores a JSON object with top-level fields `reason`, `status`. | Supporting repository file |
| [`model-artifacts.csv`](final-results/04-openvino-experimental-fork/reproduction/system/model-artifacts.csv) | CSV table with 9 data row(s). Main columns are `model_id`, `weight_format_id`, `status`, `executed_case_count`, `artifact_label`, `sha256`, `size_bytes` and 1 more. | Supporting repository file |
| [`repository.json`](final-results/04-openvino-experimental-fork/reproduction/system/repository.json) | Stores a JSON object with top-level fields `branch`, `commit`, `source_campaign`, `source_date`, `url`. | Supporting repository file |
| [`software.json`](final-results/04-openvino-experimental-fork/reproduction/system/software.json) | Stores a JSON object with top-level fields `openvino_versions`, `runtime_properties`. | Supporting repository file |

#### `final-results/04-openvino-experimental-fork/validation/`

Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the experimental OpenVINO fork route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`validation.json`](final-results/04-openvino-experimental-fork/validation/validation.json) | Machine-readable validation result for tools and automated checks. | Validation receipt |
| [`validation.md`](final-results/04-openvino-experimental-fork/validation/validation.md) | Readable explanation of the validation result and remaining limitations. | Validation receipt |

### `final-results/05-openvino-official-upstream/`

This folder covers the official upstream OpenVINO route. It is a curated publication package, not a live working directory.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`data/`](final-results/05-openvino-official-upstream/data/) | Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the official upstream OpenVINO route. |
| [`evidence/`](final-results/05-openvino-official-upstream/evidence/) | Connects published claims to the source material and checksums that support them. Here it applies to the official upstream OpenVINO route. |
| [`reports/`](final-results/05-openvino-official-upstream/reports/) | Contains human-readable result reports and editable or printable versions. Here it applies to the official upstream OpenVINO route. |
| [`reproduction/`](final-results/05-openvino-official-upstream/reproduction/) | Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the official upstream OpenVINO route. |
| [`validation/`](final-results/05-openvino-official-upstream/validation/) | Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the official upstream OpenVINO route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/05-openvino-official-upstream/README.md) | Readable Markdown document titled “Official OpenVINO upstream final results”. | Supporting repository file |

#### `final-results/05-openvino-official-upstream/data/`

Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the official upstream OpenVINO route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`attempts.csv`](final-results/05-openvino-official-upstream/data/attempts.csv) | One row per attempted configuration, including its final status. | Canonical published data |
| [`availability-matrix.csv`](final-results/05-openvino-official-upstream/data/availability-matrix.csv) | CSV table with 45 data row(s). Main columns are `test_case_id`, `model_id`, `weight_format_id`, `cache_format_id`, `status`, `executed`, `reason`. | Canonical published data |
| [`failures.csv`](final-results/05-openvino-official-upstream/data/failures.csv) | Structured failed, blocked or unavailable outcomes and their evidence references. | Canonical published data |
| [`measurements.csv`](final-results/05-openvino-official-upstream/data/measurements.csv) | Individual measured observations before route-level summarisation. | Canonical published data |
| [`quality.csv`](final-results/05-openvino-official-upstream/data/quality.csv) | Detailed quality scores at the prompt or criterion level. | Canonical published data |
| [`route.json`](final-results/05-openvino-official-upstream/data/route.json) | Machine-readable identity and scope for this route package. | Canonical published data |
| [`summaries.csv`](final-results/05-openvino-official-upstream/data/summaries.csv) | CSV table with 45 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `summary_id`, `metric_name`, `value`, `unit` and 2 more. | Canonical published data |

#### `final-results/05-openvino-official-upstream/evidence/`

Connects published claims to the source material and checksums that support them. Here it applies to the official upstream OpenVINO route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`source/`](final-results/05-openvino-official-upstream/evidence/source/) | Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the official upstream OpenVINO route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`claim-evidence-map.csv`](final-results/05-openvino-official-upstream/evidence/claim-evidence-map.csv) | Maps each published claim to the evidence that supports or limits it. | Supporting repository file |
| [`evidence-index.csv`](final-results/05-openvino-official-upstream/evidence/evidence-index.csv) | Lists evidence files and their provenance or checksum details. | Supporting repository file |
| [`manifest-sha256.txt`](final-results/05-openvino-official-upstream/evidence/manifest-sha256.txt) | SHA-256 checksums used to detect missing or changed packaged files. | Supporting repository file |
| [`source-locations.csv`](final-results/05-openvino-official-upstream/evidence/source-locations.csv) | CSV table with 94 data row(s). Main columns are `evidence_id`, `role`, `relative_path`, `sha256`, `size_bytes`, `source_label`. | Supporting repository file |

##### `final-results/05-openvino-official-upstream/evidence/source/`

Preserves source artefacts used to build this package. These are evidence, not the easiest reading copy. Here it applies to the official upstream OpenVINO route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx`](final-results/05-openvino-official-upstream/evidence/source/Granite_Official_OpenVINO_TurboQuant_Results_2026-08-30_v2_Missing_Attempts.xlsx) | Excel workbook containing Granite Official OpenVINO TurboQuant Results 2026 08 30 v2 Missing Attempts. It is preserved source evidence; use the portable copy for normal sharing. | Preserved evidence; do not edit |

#### `final-results/05-openvino-official-upstream/reports/`

Contains human-readable result reports and editable or printable versions. Here it applies to the official upstream OpenVINO route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`openvino-official-upstream-report.docx`](final-results/05-openvino-official-upstream/reports/openvino-official-upstream-report.docx) | Editable Word version of openvino official upstream report for review or handoff. | Generated or review artefact |
| [`openvino-official-upstream-report.md`](final-results/05-openvino-official-upstream/reports/openvino-official-upstream-report.md) | Readable Markdown document titled “Official upstream OpenVINO final results”. | Supporting repository file |
| [`openvino-official-upstream-report.pdf`](final-results/05-openvino-official-upstream/reports/openvino-official-upstream-report.pdf) | Printable PDF version of openvino official upstream report. Use its source file when edits are needed. | Generated or review artefact |
| [`openvino-official-upstream-results-provenance.json`](final-results/05-openvino-official-upstream/reports/openvino-official-upstream-results-provenance.json) | Stores a JSON object with top-level fields `activity_id`, `cached_formula_value_count`, `formula_count`, `formulas_preserved`, `machine_absolute_path_count`, `openpyxl_version`, `output_path`, `output_role`, …. | Supporting repository file |
| [`openvino-official-upstream-results.xlsx`](final-results/05-openvino-official-upstream/reports/openvino-official-upstream-results.xlsx) | Excel workbook containing openvino official upstream results. | Generated or review artefact |

#### `final-results/05-openvino-official-upstream/reproduction/`

Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the official upstream OpenVINO route.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`protocol/`](final-results/05-openvino-official-upstream/reproduction/protocol/) | Defines what was meant to be tested, in what order, and under which rules. Here it applies to the official upstream OpenVINO route. |
| [`quality/`](final-results/05-openvino-official-upstream/reproduction/quality/) | Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the official upstream OpenVINO route. |
| [`system/`](final-results/05-openvino-official-upstream/reproduction/system/) | Records the hardware, software and repository identity of the test environment. Here it applies to the official upstream OpenVINO route. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/05-openvino-official-upstream/reproduction/README.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |

##### `final-results/05-openvino-official-upstream/reproduction/protocol/`

Defines what was meant to be tested, in what order, and under which rules. Here it applies to the official upstream OpenVINO route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`intended-test-matrix.csv`](final-results/05-openvino-official-upstream/reproduction/protocol/intended-test-matrix.csv) | CSV table with 45 data row(s). Main columns are `test_case_id`, `model_id`, `weight_format_id`, `cache_format_id`, `intended`. | Controlled test input |

##### `final-results/05-openvino-official-upstream/reproduction/quality/`

Contains prompts, scoring rules, output indexes and quality-evaluation evidence. Here it applies to the official upstream OpenVINO route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`outputs-index.csv`](final-results/05-openvino-official-upstream/reproduction/quality/outputs-index.csv) | CSV table with 720 data row(s). Main columns are `output_id`, `test_case_id`, `prompt_id`, `domain`, `prompt_length`, `status`, `valid_output` and 4 more. | Supporting repository file |
| [`prompt-suite.csv`](final-results/05-openvino-official-upstream/reproduction/quality/prompt-suite.csv) | CSV table with 48 data row(s). Main columns are `prompt_suite_id`, `prompt_id`, `domain`, `prompt_length`, `input_evidence_id`, `relative_path`, `sha256`. | Supporting repository file |

##### `final-results/05-openvino-official-upstream/reproduction/system/`

Records the hardware, software and repository identity of the test environment. Here it applies to the official upstream OpenVINO route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`hardware.json`](final-results/05-openvino-official-upstream/reproduction/system/hardware.json) | Stores a JSON object with top-level fields `conversion_preflight_by_model_weight`, `emergency_ram_floor_bytes`, `status`. | Supporting repository file |
| [`model-artifacts.csv`](final-results/05-openvino-official-upstream/reproduction/system/model-artifacts.csv) | CSV table with 9 data row(s). Main columns are `model_id`, `weight_format_id`, `status`, `executed_case_count`, `artifact_label`, `sha256`, `size_bytes` and 1 more. | Supporting repository file |
| [`repository.json`](final-results/05-openvino-official-upstream/reproduction/system/repository.json) | Stores a JSON object with top-level fields `openvino`, `openvino_genai`, `source_campaign`, `source_date`, `turboquant_merge_commit`. | Supporting repository file |
| [`software.json`](final-results/05-openvino-official-upstream/reproduction/system/software.json) | Stores a JSON object with top-level fields `missing_model_attempt_tool_versions`, `openvino_versions`, `runtime_properties`. | Supporting repository file |

#### `final-results/05-openvino-official-upstream/validation/`

Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the official upstream OpenVINO route.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`validation.json`](final-results/05-openvino-official-upstream/validation/validation.json) | Machine-readable validation result for tools and automated checks. | Validation receipt |
| [`validation.md`](final-results/05-openvino-official-upstream/validation/validation.md) | Readable explanation of the validation result and remaining limitations. | Validation receipt |

### `final-results/06-cross-route-comparison/`

This folder covers the guarded cross-route comparison package. It is a curated publication package, not a live working directory.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`data/`](final-results/06-cross-route-comparison/data/) | Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the guarded cross-route comparison package. |
| [`evidence/`](final-results/06-cross-route-comparison/evidence/) | Connects published claims to the source material and checksums that support them. Here it applies to the guarded cross-route comparison package. |
| [`reports/`](final-results/06-cross-route-comparison/reports/) | Contains human-readable result reports and editable or printable versions. Here it applies to the guarded cross-route comparison package. |
| [`reproduction/`](final-results/06-cross-route-comparison/reproduction/) | Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the guarded cross-route comparison package. |
| [`validation/`](final-results/06-cross-route-comparison/validation/) | Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the guarded cross-route comparison package. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`README.md`](final-results/06-cross-route-comparison/README.md) | Readable Markdown document titled “Guarded cross-route comparison”. | Supporting repository file |

#### `final-results/06-cross-route-comparison/data/`

Stores the canonical machine-readable rows used for analysis and reporting. Here it applies to the guarded cross-route comparison package.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`comparability-matrix.csv`](final-results/06-cross-route-comparison/data/comparability-matrix.csv) | CSV table with 20 data row(s). Main columns are `left_route_id`, `right_route_id`, `metric`, `classification`, `reason_codes_json`, `reason_details_json`, `matched_case_count` and 6 more. | Canonical published data |
| [`route-status-summary.csv`](final-results/06-cross-route-comparison/data/route-status-summary.csv) | CSV table with 169 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `attempt_id`, `status`, `executed`, `reason` and 7 more. | Canonical published data |
| [`route.json`](final-results/06-cross-route-comparison/data/route.json) | Machine-readable identity and scope for this route package. | Canonical published data |

#### `final-results/06-cross-route-comparison/evidence/`

Connects published claims to the source material and checksums that support them. Here it applies to the guarded cross-route comparison package.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`claim-evidence-map.csv`](final-results/06-cross-route-comparison/evidence/claim-evidence-map.csv) | Maps each published claim to the evidence that supports or limits it. | Supporting repository file |
| [`manifest-sha256.txt`](final-results/06-cross-route-comparison/evidence/manifest-sha256.txt) | SHA-256 checksums used to detect missing or changed packaged files. | Supporting repository file |

#### `final-results/06-cross-route-comparison/reports/`

Contains human-readable result reports and editable or printable versions. Here it applies to the guarded cross-route comparison package.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`cross-route-comparison-report.docx`](final-results/06-cross-route-comparison/reports/cross-route-comparison-report.docx) | Editable Word version of cross route comparison report for review or handoff. | Generated or review artefact |
| [`cross-route-comparison-report.md`](final-results/06-cross-route-comparison/reports/cross-route-comparison-report.md) | Readable Markdown document titled “Guarded cross-route comparison”. | Supporting repository file |
| [`cross-route-comparison-report.pdf`](final-results/06-cross-route-comparison/reports/cross-route-comparison-report.pdf) | Printable PDF version of cross route comparison report. Use its source file when edits are needed. | Generated or review artefact |

#### `final-results/06-cross-route-comparison/reproduction/`

Contains the inputs and instructions needed to understand or reproduce the route. Here it applies to the guarded cross-route comparison package.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`protocol/`](final-results/06-cross-route-comparison/reproduction/protocol/) | Defines what was meant to be tested, in what order, and under which rules. Here it applies to the guarded cross-route comparison package. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`commands.md`](final-results/06-cross-route-comparison/reproduction/commands.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |
| [`README.md`](final-results/06-cross-route-comparison/reproduction/README.md) | Readable Markdown document titled “Release verification command”. | Supporting repository file |

##### `final-results/06-cross-route-comparison/reproduction/protocol/`

Defines what was meant to be tested, in what order, and under which rules. Here it applies to the guarded cross-route comparison package.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`comparability-policy.md`](final-results/06-cross-route-comparison/reproduction/protocol/comparability-policy.md) | Readable Markdown document titled “Comparability policy”. | Controlled test input |

#### `final-results/06-cross-route-comparison/validation/`

Contains checks showing whether the package structure and claims satisfy the release rules. Here it applies to the guarded cross-route comparison package.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`validation.json`](final-results/06-cross-route-comparison/validation/validation.json) | Machine-readable validation result for tools and automated checks. | Validation receipt |
| [`validation.md`](final-results/06-cross-route-comparison/validation/validation.md) | Readable explanation of the validation result and remaining limitations. | Validation receipt |

### `final-results/catalog/`

Combines route-level records into collection-wide tables.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`campaign-summary.csv`](final-results/catalog/campaign-summary.csv) | CSV table with 169 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `attempt_id`, `status`, `executed`, `reason` and 7 more. | Supporting repository file |
| [`claim-evidence-map.csv`](final-results/catalog/claim-evidence-map.csv) | Maps each published claim to the evidence that supports or limits it. | Supporting repository file |
| [`comparability-matrix.csv`](final-results/catalog/comparability-matrix.csv) | CSV table with 20 data row(s). Main columns are `left_route_id`, `right_route_id`, `metric`, `classification`, `reason_codes_json`, `reason_details_json`, `matched_case_count` and 6 more. | Supporting repository file |
| [`evidence-manifest.csv`](final-results/catalog/evidence-manifest.csv) | CSV table with 1857 data row(s). Main columns are `route_id`, `campaign_id`, `evidence_id`, `role`, `relative_path`, `sha256`, `size_bytes` and 3 more. | Supporting repository file |
| [`failure-summary.csv`](final-results/catalog/failure-summary.csv) | CSV table with 93 data row(s). Main columns are `route_id`, `campaign_id`, `test_case_id`, `attempt_id`, `failure_id`, `status`, `stage` and 3 more. | Supporting repository file |
| [`performance-summary.csv`](final-results/catalog/performance-summary.csv) | Normalised performance values used for route-level reporting. | Supporting repository file |
| [`quality-summary.csv`](final-results/catalog/quality-summary.csv) | One quality summary per eligible configuration. | Supporting repository file |
| [`route-register.csv`](final-results/catalog/route-register.csv) | CSV table with 5 data row(s). Main columns are `route_id`, `campaign_id`, `attempt_count`, `measurement_count`, `summary_count`, `quality_count`, `failure_count` and 1 more. | Supporting repository file |

### `final-results/standards/`

Defines shared formats and rules used by every final-results package.

**Folders at this level**

| Folder | Purpose |
| --- | --- |
| [`schemas/`](final-results/standards/schemas/) | Contains machine-readable rules that describe valid evidence files. |

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`data-dictionary.md`](final-results/standards/data-dictionary.md) | Readable Markdown document titled “Data dictionary”. | Supporting repository file |
| [`metric-definitions.md`](final-results/standards/metric-definitions.md) | Readable Markdown document titled “Metric definitions”. | Supporting repository file |
| [`provenance-policy.md`](final-results/standards/provenance-policy.md) | Readable Markdown document titled “Provenance policy”. | Supporting repository file |
| [`quality-comparison-policy.md`](final-results/standards/quality-comparison-policy.md) | Readable Markdown document titled “Quality comparison policy”. | Supporting repository file |
| [`README.md`](final-results/standards/README.md) | Readable Markdown document titled “Final-results standards”. | Supporting repository file |
| [`status-taxonomy.md`](final-results/standards/status-taxonomy.md) | Readable Markdown document titled “Status taxonomy”. | Supporting repository file |

#### `final-results/standards/schemas/`

Contains machine-readable rules that describe valid evidence files.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`attempts.schema.json`](final-results/standards/schemas/attempts.schema.json) | Stores a JSON object with top-level fields `$schema`, `title`, `type`, `additionalProperties`, `required`, `properties`, `allOf`. | Supporting repository file |
| [`evidence.schema.json`](final-results/standards/schemas/evidence.schema.json) | Stores a JSON object with top-level fields `$schema`, `title`, `type`, `additionalProperties`, `required`, `properties`, `allOf`. | Supporting repository file |
| [`failures.schema.json`](final-results/standards/schemas/failures.schema.json) | Stores a JSON object with top-level fields `$schema`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`measurements.schema.json`](final-results/standards/schemas/measurements.schema.json) | Stores a JSON object with top-level fields `$schema`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`quality.schema.json`](final-results/standards/schemas/quality.schema.json) | Stores a JSON object with top-level fields `$schema`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`results.schema.json`](final-results/standards/schemas/results.schema.json) | Stores a JSON object with top-level fields `$schema`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`route-manifest.schema.json`](final-results/standards/schemas/route-manifest.schema.json) | Stores a JSON object with top-level fields `$schema`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |

### `final-results/validation/`

Contains checks showing whether the package structure and claims satisfy the release rules.

**Files at this level**

| File | What it contains or does | Authority and editing guidance |
| --- | --- | --- |
| [`cross-route-validation.json`](final-results/validation/cross-route-validation.json) | Stores a JSON object with top-level fields `gates`, `root`, `scope`, `valid`. | Validation receipt |
| [`integrity-validation.json`](final-results/validation/integrity-validation.json) | Stores a JSON object with top-level fields `gates`, `root`, `scope`, `valid`. | Validation receipt |
| [`README.md`](final-results/validation/README.md) | Readable Markdown document titled “Validation receipts”. | Validation receipt |
| [`release-readiness.json`](final-results/validation/release-readiness.json) | Machine-readable receipt for the final release-readiness checks. | Validation receipt |
| [`schema-validation.json`](final-results/validation/schema-validation.json) | Stores a JSON object with top-level fields `gates`, `root`, `scope`, `valid`. | Validation receipt |
| [`validation-summary.md`](final-results/validation/validation-summary.md) | Readable Markdown document titled “Unified final-results validation summary”. | Validation receipt |
| [`validation.json`](final-results/validation/validation.json) | Machine-readable validation result for tools and automated checks. | Validation receipt |
| [`validation.md`](final-results/validation/validation.md) | Readable explanation of the validation result and remaining limitations. | Validation receipt |

## Relationship to the working experiment tree

The `experiments/` tree contains protocols, manifests, raw outputs and intermediate processing material. The final-results package is the smaller publication layer built from that evidence. Start here for conclusions; move to `experiments/` only when you need to audit or reproduce how a value was produced.

## Related guides

- [Controlled testing workspace](README.md)
- [Final-results release guide](final-results/README.md)
- [Testing command guide](../../scripts/testing/README.md)
- [Experiment library guide](../../experiments/README.md)
