# Workbook 05

Contains controlled helpers for Workbook 05 source admission, build and measurement stages.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains controlled helpers for Workbook 05 source admission, build and measurement stages.

### Start here

Begin with [`build-binary-record.schema.json`](build-binary-record.schema.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`build-binary-record.schema.json`](build-binary-record.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`build-command-record.schema.json`](build-command-record.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`build-compatibility-attempt.schema.json`](build-compatibility-attempt.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`build-decision.schema.json`](build-decision.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`build-dependency-record.schema.json`](build-dependency-record.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`build-deviation-record.schema.json`](build-deviation-record.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`build-resource-summary.schema.json`](build-resource-summary.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`campaign-manifest.schema.json`](campaign-manifest.schema.json) | Stores a JSON object with top-level fields `$schema`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`checkpoint.schema.json`](checkpoint.schema.json) | Stores a JSON object with top-level fields `$schema`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`cmake-test-discovery-report.schema.json`](cmake-test-discovery-report.schema.json) | Stores a JSON object with top-level fields `$schema`, `$defs`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`configure-probe-report.schema.json`](configure-probe-report.schema.json) | Stores a JSON object with top-level fields `$schema`, `$defs`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`conversion-dependency-preflight.schema.json`](conversion-dependency-preflight.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `allOf`, `$defs`, …. | Supporting repository file |
| [`documented-command-manifest.schema.json`](documented-command-manifest.schema.json) | Stores a JSON object with top-level fields `$schema`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`measured-run-manifest.schema.json`](measured-run-manifest.schema.json) | Stores a JSON object with top-level fields `$schema`, `$defs`, `type`, `additionalProperties`, `required`, `properties`, `allOf`. | Supporting repository file |
| [`measurement-controls-report.schema.json`](measurement-controls-report.schema.json) | Stores a JSON object with top-level fields `$schema`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`model-asset-lock.schema.json`](model-asset-lock.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `$defs`, `properties`, …. | Supporting repository file |
| [`model-conversion-record.schema.json`](model-conversion-record.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `$defs`, `properties`, …. | Supporting repository file |
| [`phase3-prerequisite-proof.schema.json`](phase3-prerequisite-proof.schema.json) | Stores a JSON object with top-level fields `$schema`, `$id`, `title`, `type`, `additionalProperties`, `required`, `$defs`, `properties`. | Supporting repository file |
| [`preflight-report.schema.json`](preflight-report.schema.json) | Stores a JSON object with top-level fields `$schema`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`source-admission-summary.schema.json`](source-admission-summary.schema.json) | Stores a JSON object with top-level fields `$schema`, `$defs`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`source-admission.schema.json`](source-admission.schema.json) | Stores a JSON object with top-level fields `$schema`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`source-capability-report.schema.json`](source-capability-report.schema.json) | Stores a JSON object with top-level fields `$schema`, `$defs`, `type`, `additionalProperties`, `required`, `properties`. | Supporting repository file |
| [`source-tree-report.schema.json`](source-tree-report.schema.json) | Stores a JSON object with top-level fields `$schema`, `$defs`, `type`, `additionalProperties`, `required`, `properties`, `allOf`. | Supporting repository file |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
