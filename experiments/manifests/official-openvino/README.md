# Official OpenVINO Manifests

Store one immutable manifest per run using `test-id/run-id/manifest.json`.

The formal retest is pinned by `retest-matrix.json` to OpenVINO `2026.2.1` and
OpenVINO GenAI `2026.2.1.0`. Acquire the isolated Python environment, exact
release-tag source checkouts, hashes, package inventory, and device inventory:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/testing/acquire_official_openvino.ps1
```

Machine-local dependencies are placed in `.venv-official-openvino-2026.2.1/`
and `external/official-openvino/2026-07-19/`; both are ignored by Git. Auditable
outputs are written beneath
`experiments/raw-results/official-openvino/2026-07-19/{acquisition,environment}/`.
The script is resumable, rejects mismatched tags/remotes, dirty checkouts,
incorrect package versions, and a missing OpenVINO CPU or GPU device.

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This folder covers the official OpenVINO route.

### Start here

Begin with [`adaptive-format-comparison-matrix-v1.json`](adaptive-format-comparison-matrix-v1.json). The tables below explain the remaining items.

### How this folder fits into testing

This folder belongs to the experiment layer between the test plan and the curated final-results release.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`adaptive-format-comparison-matrix-v1.json`](adaptive-format-comparison-matrix-v1.json) | Stores a JSON object with top-level fields `build_identity`, `cases`, `source_identity`. | Controlled test input |
| [`format-boundary-matrix-v1.json`](format-boundary-matrix-v1.json) | Stores a JSON object with top-level fields `schema`, `cases`. | Controlled test input |
| [`retest-matrix.json`](retest-matrix.json) | Stores a JSON object with top-level fields `schema_version`, `openvino_release`, `openvino_genai_release`, `formal_metrics`, `source_identity`, `build_identity`, `cases`. | Controlled test input |

### Important boundaries

- A protocol or manifest describes intended work; it is not proof that the experiment ran.
- Use the curated final-results package for conclusions and this tree for audit or reproduction.

### Related guides

- [Parent guide](../README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
