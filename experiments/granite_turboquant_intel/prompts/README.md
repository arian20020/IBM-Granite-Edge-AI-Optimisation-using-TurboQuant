# Prompts

Contains fixed prompts used to make model-quality tests repeatable.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

Contains fixed prompts used to make model-quality tests repeatable.

### Start here

Begin with [`compact-feasibility-prompt-set-v2.json`](compact-feasibility-prompt-set-v2.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Folders

| Folder | What it contains |
| --- | --- |
| [`fixtures/`](fixtures/README.md) | Contains small controlled inputs used by automated tests. |
| [`rendered/`](rendered/README.md) | Contains prompts rendered into the exact form sent to a model. |
| [`rendered-v2/`](rendered-v2/README.md) | Contains the second controlled version of rendered prompts. |

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`compact-feasibility-prompt-set-v2.json`](compact-feasibility-prompt-set-v2.json) | Stores a JSON object with top-level fields `prompt_set_id`, `version`, `status`, `created_date`, `purpose`, `claim_boundary`, `maximum_input_tokens`, `rendered_asset_manifest`, …. | Controlled test input |
| [`fixed-feasibility-prompt-set-v1.json`](fixed-feasibility-prompt-set-v1.json) | Stores a JSON object with top-level fields `prompt_set_id`, `version`, `status`, `created_date`, `purpose`, `claim_boundary`, `generation_defaults`, `prompts`. | Controlled test input |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)
- [fixtures guide](fixtures/README.md)
- [rendered guide](rendered/README.md)
- [rendered-v2 guide](rendered-v2/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
