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
