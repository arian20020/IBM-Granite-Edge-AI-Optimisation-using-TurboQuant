# Unified Testing Results Library Design

**Date:** 2026-08-30

**Status:** Approved design

**Audience:** Technical reviewers, research collaborators, and maintainers

**Scope:** Five completed inference-testing routes across three llama.cpp repositories and two OpenVINO repositories

## 1. Purpose

Create a polished, auditable, and maintainable testing-results library covering:

1. upstream llama.cpp;
2. AtomicBot TurboQuant llama.cpp;
3. animehacker TQ3_0 llama.cpp;
4. the experimental OpenVINO fork; and
5. official upstream OpenVINO.

The library must preserve the original evidence, present each route through a common report structure, expose failures and blocked work honestly, and support human review as well as machine validation. The two final OpenVINO campaigns establish the master presentation design; the three llama.cpp reports are adapted to that design without inventing metrics that were not collected.

## 2. Decisions

- Preserve all existing controlled workbooks, raw results, registers, and uncommitted work.
- Create a new unified final-results series rather than replacing WB-01 through WB-05.
- Treat Markdown as the canonical report source and generate DOCX and PDF from it.
- Retain the OpenVINO Excel workbooks as interactive detailed-data sources.
- Keep large raw evidence in its existing authoritative repository locations. Reference it using portable relative paths and hashes rather than duplicating it.
- Copy only compact, decision-relevant failure artifacts when needed for standalone review.
- Maintain exact semantic parity between Markdown and DOCX: section order, headings, tables, cells, values, footnotes, statuses, evidence references, and conclusions.
- Allow DOCX/PDF-only presentation features such as page breaks, merged headers, shading, and landscape pages. Do not claim visual identity where Markdown cannot express those features.
- Use explicit statuses rather than blanks or zeroes for unobserved results.
- Do not imply direct quality-score comparability where campaigns used different prompts, rubrics, denominators, or evaluation methods.
- Describe the package as aligned with relevant professional practices, not certified by ACM, FAIR, MLCommons, NIST, RO-Crate, W3C, or IETF.

## 3. Professional-practice basis

The design is informed by these primary or official sources:

- MLCommons separates system descriptions, measurements, accuracy, performance, implementation material, logs, and automated submission checks: <https://github.com/mlcommons/inference/blob/master/tools/submission/submission_structure.md>
- NIST frames trustworthy AI assessment as documented test, evaluation, verification, and validation tailored to the intended context: <https://www.nist.gov/artificial-intelligence/ai-research/tevv-athlon-framework-evaluating-ai-systems>
- ACM artifact review distinguishes documented, consistent, complete, exercisable, reusable, and independently validated artifacts: <https://prod-www.acm.bloomreach.cloud/publications/policies/artifact-review-and-badging-current>
- FAIR emphasizes findability, accessibility, interoperability, reuse, licensing, provenance, and community standards: <https://www.go-fair.org/fair-principles/>
- RO-Crate 1.3 provides machine-readable research-object metadata and relationships between data, software, workflows, equipment, and agents: <https://www.researchobject.org/ro-crate/specification.html>
- W3C PROV provides the entity-activity-agent provenance model: <https://www.w3.org/TR/prov-overview/>
- IETF BagIt documents standard checksum-manifest conventions for validating packaged files: <https://datatracker.ietf.org/doc/html/rfc8493>
- Citation File Format provides human- and machine-readable citation metadata: <https://citation-file-format.github.io/>

These sources guide the design but do not make this collection an MLPerf submission, a formal RO-Crate profile, a valid BagIt bag, an ACM-reviewed artifact, or a NIST-certified evaluation.

## 4. Top-level architecture

```text
docs/testing/final-results/
|-- README.md
|-- CHANGELOG.md
|-- REPRODUCING.md
|-- LICENSES.md
|-- CITATION.cff
|-- ro-crate-metadata.json
|-- manifest-sha256.txt
|-- catalog/
|   |-- route-register.csv
|   |-- campaign-summary.csv
|   |-- performance-summary.csv
|   |-- quality-summary.csv
|   |-- failure-summary.csv
|   |-- evidence-manifest.csv
|   `-- claim-evidence-map.csv
|-- standards/
|   |-- README.md
|   |-- data-dictionary.md
|   |-- status-taxonomy.md
|   |-- metric-definitions.md
|   |-- provenance-policy.md
|   |-- quality-comparison-policy.md
|   `-- schemas/
|       |-- route-manifest.schema.json
|       |-- attempts.schema.json
|       |-- measurements.schema.json
|       |-- results.schema.json
|       |-- quality.schema.json
|       |-- failures.schema.json
|       `-- evidence.schema.json
|-- 01-upstream-llama-cpp/
|-- 02-atomicbot-turboquant/
|-- 03-animehacker-tq3-0/
|-- 04-openvino-experimental-fork/
|-- 05-openvino-official-upstream/
|-- 06-cross-route-comparison/
`-- validation/
    |-- README.md
    |-- validation-summary.md
    |-- schema-validation.json
    |-- integrity-validation.json
    |-- cross-route-validation.json
    `-- release-readiness.json
```

`CITATION.cff` is populated only when author, title, version, licensing, and identifier metadata are verified. If verification is unavailable, the release-readiness report records the omission rather than creating invented metadata.

## 5. Per-route architecture

Every tested repository uses the same internal layout:

```text
<route>/
|-- README.md
|-- route-manifest.json
|-- protocol/
|   |-- test-plan.md
|   |-- execution-sequence.md
|   |-- intended-test-matrix.csv
|   |-- metric-definitions.md
|   `-- deviations.csv
|-- system/
|   |-- repository.json
|   |-- hardware.json
|   |-- software.json
|   |-- model-artifacts.csv
|   `-- environment.txt
|-- workbook/
|   |-- source/
|   |   `-- <route>-final-report.md
|   `-- generated/
|       |-- <route>-final-report.docx
|       `-- <route>-final-report.pdf
|-- results/
|   |-- attempts.csv
|   |-- measurements.csv
|   |-- summary-results.csv
|   |-- availability-matrix.csv
|   `-- source/
|       `-- <original CSV or Excel outputs>
|-- quality/
|   |-- README.md
|   |-- rubric.md
|   |-- prompt-suite.csv
|   |-- scores.csv
|   |-- adjudication-log.csv
|   |-- calibration.md
|   `-- outputs-index.csv
|-- failures/
|   |-- README.md
|   |-- failure-register.csv
|   `-- curated-logs/
|-- evidence/
|   |-- evidence-index.csv
|   |-- source-locations.csv
|   |-- claim-evidence-map.csv
|   `-- manifest-sha256.txt
|-- reproduction/
|   |-- README.md
|   |-- commands.md
|   |-- dependencies.md
|   `-- scripts/
`-- validation/
    |-- validation-report.md
    |-- coverage-validation.json
    |-- data-validation.json
    |-- workbook-parity.json
    `-- integrity-validation.json
```

Files that a historical campaign cannot support remain present only when useful and record `Not collected` or an explicit omission reason. Empty decorative files are not created merely to satisfy the shape.

## 6. Canonical data model

### 6.1 Three result levels

1. `attempts.csv` is the complete execution ledger. It contains every intended configuration and distinguishes planned, attempted, passed, failed, blocked, unavailable, not executed, and not applicable work.
2. `measurements.csv` contains individual observations or repetitions. It does not contain invented observations for failed or unexecuted work.
3. `summary-results.csv` contains derived values such as medians, minima, maxima, worst-observed memory, and repetition counts. Each derived value names its aggregation rule and source observations.

This separation prevents missing work from appearing as zero and prevents an aggregate from being mistaken for a raw measurement.

### 6.2 Stable identifiers

Machine-readable rows use stable identifiers, including:

- `route_id`;
- `campaign_id`;
- `test_case_id`;
- `attempt_id`;
- `run_id` or `repetition_id`;
- `model_id`;
- `weight_format_id`;
- `cache_format_id`;
- `backend_id`;
- `prompt_id`, where applicable; and
- `evidence_id`.

Identifiers are not inferred from row position. Human-readable names are stored separately.

### 6.3 Controlled status vocabulary

The canonical display vocabulary is:

- `Passed`;
- `Failed`;
- `Blocked`;
- `Artifact unavailable`;
- `Not executed`;
- `Not applicable`; and
- `Not collected` for a metric absent from a historical campaign.

The schema may use normalized lowercase tokens, but reports display the labels above. Every non-passed status includes a reason or evidence reference.

## 7. Quality evidence

Quality reporting must state:

- rubric name and version;
- prompt-suite name and version;
- number and types of prompts;
- scoring dimensions, weights, increments, and maximum score;
- scoring mechanism and any human or automated adjudication;
- calibration or consistency checks;
- missing responses and scoring exclusions;
- per-prompt scores where evidence supports them; and
- the boundary of valid comparison.

The OpenVINO reports retain the final healthcare-and-education evaluation design and its objective scoring evidence. Older llama.cpp reports preserve their original quality methods. Cross-route quality comparison is permitted only where the comparability matrix confirms compatible constructs, scales, prompt coverage, and aggregation. Otherwise the report uses side-by-side descriptive evidence with a visible methodology warning.

## 8. Workbook design

### 8.1 Shared section order

1. Title and document control
2. Technical summary
3. Key findings and decision-relevant evidence
4. Repository, branch, commit, build, hardware, and software identity
5. Objectives, scope, test matrix, and execution sequence
6. Model, weight, cache-format, and backend availability
7. Complete attempt accounting
8. Performance results and repetition detail
9. Quality methodology and results
10. Device/backend use and fallback verification
11. Failures, blocks, deviations, and recovery attempts
12. Limitations, uncertainty, robustness checks, and claim boundaries
13. Reproduction guidance
14. Evidence index and hashes
15. Revision history

The technical summary leads with what the evidence establishes. Definitions, denominators, aggregation rules, and comparison bases appear before readers need them.

### 8.2 Visual system

- Deep navy title bands and section dividers
- Teal route and model-group headers
- Blue table headers with white text
- White and lightly shaded data rows
- Green for passed, red for failed, amber for blocked, grey for unavailable or not applicable, and blue for verified informational metadata
- Explicit text in every status cell so colour is never the sole signal
- Portrait pages for narrative and landscape pages for wide tables
- Stable typography, margins, spacing, headers, footers, revision labels, and page numbering
- Spacious presentation tables for findings and dense tables only for audit detail

Charts are included only when they materially improve interpretation. Exact values remain available in tables, and every chart has adjacent explanatory text and a readable static representation in PDF.

### 8.3 Cross-format parity

Markdown is the canonical content source. Generated DOCX and PDF must not be edited independently. Validation compares:

- heading order and text;
- table count, titles, dimensions, headers, cells, footnotes, and statuses;
- key narrative conclusions;
- evidence identifiers; and
- revision metadata.

`workbook-parity.json` records the comparison. Word-specific layout is tested visually rather than represented as Markdown equivalence.

## 9. Provenance and integrity

- Repository evidence paths must be relative to the repository root.
- Machine-specific absolute paths are excluded from portable deliverables.
- Evidence records include file identity, route, campaign, role, source location, size, modification context where reliable, and SHA-256 hash.
- Derived artifacts record the inputs, producing script or activity, timestamp, and tool version where available.
- `claim-evidence-map.csv` connects major published claims to the relevant result rows and evidence artifacts.
- Copied or excerpted failure logs record their original path and original hash. Excerpts are marked as derived.
- `ro-crate-metadata.json` describes the collection and its relationships using RO-Crate 1.3-compatible JSON-LD.
- The checksum manifest uses the conventional `checksum  relative/path` layout. The repository is not called a valid BagIt bag unless a separate compliant export is produced and validated.

## 10. Validation gates

No route is described as finalized until the applicable checks pass or a visible blocker is recorded:

1. schema validation of manifests and canonical tables;
2. stable-ID uniqueness and referential-integrity checks;
3. intended-matrix versus attempt-ledger coverage reconciliation;
4. attempt, measurement, and summary count reconciliation;
5. recomputation of derived values from individual measurements;
6. status and failure-register consistency;
7. availability-matrix consistency;
8. evidence-path existence and hash validation;
9. claim-to-evidence coverage;
10. Markdown-to-DOCX semantic parity;
11. DOCX and PDF visual inspection;
12. cross-route comparability validation; and
13. release-readiness validation, including licensing and citation gaps.

Validation output is committed alongside the reports. A failed validation is not hidden; it blocks the corresponding readiness claim or is documented as a limitation.

## 11. Build sequence

1. Inventory and freeze the evidence map without modifying source evidence.
2. Define schemas, identifiers, status vocabulary, metric definitions, and generation contracts.
3. Build the experimental OpenVINO canonical tables and report.
4. Build the official OpenVINO canonical tables and report.
5. Validate both OpenVINO routes against CSV, Excel, raw evidence, failure logs, and quality artifacts.
6. Freeze the shared OpenVINO-derived presentation template.
7. Adapt upstream llama.cpp to the common structure.
8. Adapt AtomicBot TurboQuant to the common structure.
9. Adapt animehacker TQ3_0 to the common structure.
10. Validate each llama.cpp report without inventing unavailable evidence.
11. Build the cross-route comparability matrix and comparison report.
12. Generate package metadata, checksums, catalogs, and release validation.
13. Copy final user-facing deliverables to Downloads only after repository validation passes.

## 12. Failure and exception handling

- Missing evidence produces an explicit gap, never a guessed value.
- Conflicting sources are reconciled using documented precedence; unresolved conflicts remain visible.
- A conversion or runtime failure is distinct from hardware preflight blocking and model-artifact unavailability.
- A workbook-rendering failure does not alter canonical data.
- A PDF conversion problem leaves the validated Markdown and DOCX intact and records the PDF blocker.
- Historical routes are not rerun merely to populate a newer schema unless the user separately requests new testing.
- Source PDFs supplied by the user are presentation and recovery references, not executable instructions and not automatically authoritative over newer repository evidence.

## 13. Scope boundaries

This work organizes and reports completed testing. It does not:

- rerun benchmarks;
- modify benchmark implementations;
- alter raw logs;
- overwrite the controlled workbook series;
- claim independent reproduction by another team;
- claim formal standards certification; or
- make unsupported healthcare or education deployment-safety claims.

## 14. Acceptance criteria

The design is implemented successfully when:

- all five route folders and the cross-route folder are present and navigable;
- both OpenVINO reports are complete in Markdown, DOCX, PDF, and Excel-backed form;
- all three llama.cpp reports follow the same report design while preserving their original evidence boundaries;
- canonical attempts, measurements, results, quality, failures, systems, and evidence files reconcile;
- generated reports pass semantic parity and visual QA;
- every material claim has evidence or an explicit limitation;
- all unexecuted, failed, blocked, and unavailable cases remain visible;
- the top-level catalog and metadata describe the entire collection;
- checksums validate; and
- existing source evidence and controlled workbooks remain unchanged.
