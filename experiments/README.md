# Experiments

Every formal experiment needs an ID, protocol, manifest, exact hashes, raw stdout/stderr, raw measurements, failed runs and reproducible processing scripts.

## Choose what you need

| Goal | Open this first |
| --- | --- |
| Read validated conclusions | [Final results](../docs/testing/final-results/README.md) |
| Find the procedure that was intended | [Protocols](protocols/README.md) |
| Check exact versions, inputs and configuration | [Manifests](manifests/README.md) |
| Inspect direct captured evidence | [Raw results](raw-results/README.md) |
| Inspect calculated or normalised outputs | [Processed results](processed-results/README.md) |
| Understand prompts and quality scoring | [Prompts](prompts/README.md) and [rubrics](rubrics/README.md) |
| Find charts used in reports | [Figures](figures/README.md) |
| Inspect controlled source patches | [Patches](patches/README.md) |
| Find route-specific compatibility scripts | [Experiment scripts](scripts/README.md) |

A protocol, manifest, script or empty folder proves only that testing was prepared. A result becomes usable only when its captured evidence, provenance and validation agree.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This is the experiment library: definitions, raw evidence, processed results and supporting tools.

### Start here

Use [`../docs/testing/final-results/`](../docs/testing/final-results/README.md) for conclusions. Use this tree when you need protocols, raw evidence or processing details.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| [`figures/`](figures/README.md) | Contains generated charts used to explain test results. |
| [`granite_turboquant_intel/`](granite_turboquant_intel/README.md) | Contains the operational evidence workspace for the controlled Granite and TurboQuant campaigns. |
| [`manifests/`](manifests/README.md) | Records the exact inputs, versions and intended configurations for experiments. |
| [`patches/`](patches/README.md) | Contains controlled patch inputs used by selected experimental builds. |
| [`processed-results/`](processed-results/README.md) | Contains results derived from raw evidence. Use validation and provenance before trusting a value. |
| [`prompts/`](prompts/README.md) | Contains fixed prompts used to make model-quality tests repeatable. |
| [`protocols/`](protocols/README.md) | Defines experiment procedures, gates and stopping rules. |
| [`raw-results/`](raw-results/README.md) | Preserves direct outputs from test runs. Start with final-results for conclusions. |
| [`rubrics/`](rubrics/README.md) | Defines how model output quality is scored. |
| [`scripts/`](scripts/README.md) | Contains route-specific experiment scripts and compatibility entry points. |

### Files

There are no immediate non-README files at this level. Continue into the child folders described above.

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [figures guide](figures/README.md)
- [granite_turboquant_intel guide](granite_turboquant_intel/README.md)
- [manifests guide](manifests/README.md)
- [patches guide](patches/README.md)
- [processed-results guide](processed-results/README.md)
- [prompts guide](prompts/README.md)
- [protocols guide](protocols/README.md)
- [raw-results guide](raw-results/README.md)
- [rubrics guide](rubrics/README.md)
- [scripts guide](scripts/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
