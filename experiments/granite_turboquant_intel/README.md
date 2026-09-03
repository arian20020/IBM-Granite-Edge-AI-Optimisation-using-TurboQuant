# Granite and TurboQuant Intel Evidence Campaign

This is the formal evidence root required by the supplied operational testing procedure.

```text
experiments/granite_turboquant_intel/
├── manifests/       # machine, repository, build, model, configuration and run identities
├── configurations/  # frozen command/configuration files
├── scripts/         # versioned build, run, capture and processing scripts
├── logs/            # immutable stdout, stderr, runtime and device logs
├── outputs/         # original model responses and converted artefact inventories
├── metrics/         # raw samples and processed performance/memory data
├── results/         # validated summaries and comparison tables
├── notes/           # dated diagnostic notes and bounded interpretations
├── prompts/         # frozen prompt set and deterministic fixtures
└── rubrics/         # controlling quality rubric
```

## Run layout

Use:

`<evidence-category>/<route>/<test-id>/<run-id>/...`

Example:

`logs/upstream-llama-cpp/UL-04/UL-04-R001/stdout.log`

## Immutability rule

Raw command, output, log and sample files are not edited after capture. Corrections are new processed files or new run IDs. Every final evidence directory receives a SHA-256 manifest before its workbook row is marked complete.

## Exclusions

Do not commit model weights, converted model folders, third-party repositories, build caches, executables, DLLs, secrets or private/sensitive user data. Record their provenance, revisions and hashes instead.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains the operational evidence workspace for the controlled Granite and TurboQuant campaigns.

### Start here

Begin with [`.gitignore`](.gitignore). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| [`configurations/`](configurations/README.md) | Contains fixed configuration inputs used by controlled tests. |
| [`logs/`](logs/README.md) | Contains captured console and diagnostic output from controlled runs. |
| [`manifests/`](manifests/README.md) | Records the exact inputs, versions and intended configurations for experiments. |
| [`notes/`](notes/README.md) | Contains operator notes that add context but do not replace machine-readable evidence. |
| [`processed-results/`](processed-results/README.md) | Contains results derived from raw evidence. Use validation and provenance before trusting a value. |
| [`prompts/`](prompts/README.md) | Contains fixed prompts used to make model-quality tests repeatable. |
| [`rubrics/`](rubrics/README.md) | Defines how model output quality is scored. |
| [`schemas/`](schemas/README.md) | Contains machine-readable rules that describe valid evidence files. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`.gitignore`](.gitignore) | Supporting data file for .gitignore. | Supporting repository file |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)
- [configurations guide](configurations/README.md)
- [logs guide](logs/README.md)
- [manifests guide](manifests/README.md)
- [notes guide](notes/README.md)
- [processed-results guide](processed-results/README.md)
- [prompts guide](prompts/README.md)
- [rubrics guide](rubrics/README.md)
- [schemas guide](schemas/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
