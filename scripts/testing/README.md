# Testing Commands

`scripts/testing` now exposes the supported contributor-facing command surface for testing campaigns and final-results validation. The CLI layer stays thin: it dispatches to the proven campaign and reporting implementations without duplicating route logic.

The four `Validate-Workbook05-*.ps1` files remain top-level, documented CI entrypoints supplied by current `main`; implementation helpers remain grouped under `workbook05/` and `tools/`.

## Safety boundaries

- `python -m scripts.testing.cli.validate_results` is read-only. It validates an existing final-results tree and returns `0` for pass or `1` for validation findings.
- `python -m scripts.testing.cli.build_results` can mutate a new output tree. It refuses the protected repository output root, refuses output paths outside the repository, and refuses non-empty output directories.
- Route runners can launch model servers, write evidence, and create or resume campaign outputs. They do not archive, delete, or rewrite historical evidence outside the selected command's own output roots.
- Every CLI supports `--help`. Invalid wrapper use exits `2`, and the moved results builder preserves exit codes `0`, `1`, and `2`.

## Supported commands

- `python -m scripts.testing.cli.run_llama --mode <measure-run|measure-server> -- ...`
- `python -m scripts.testing.cli.run_atomicbot --mode <retest|server-metrics|full-quality> -- ...`
- `python -m scripts.testing.cli.run_animehacker --mode <retest|quality|large-host> -- ...`
- `python -m scripts.testing.cli.run_openvino --mode <retest|quality|reference-capability|diagnostics|adaptive-quality|adaptive-comparison|format-boundary> -- ...`
- `python -m scripts.testing.cli.build_results --route <all|openvino|experimental-openvino|official-openvino|upstream-llama|atomicbot|animehacker|cross-route> --output-root <path> [--validate-only]`
- `python -m scripts.testing.cli.validate_results --route <all|openvino|experimental-openvino|official-openvino|upstream-llama|atomicbot|animehacker|cross-route> --output-root <path>`
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/testing/cli/export_report.ps1 -DocxPath <docx> -PdfPath <pdf> -TimeoutSeconds 180`

Use `--` before forwarded route-specific options so the wrapper passes them through unchanged to the selected underlying command.

## Examples

```powershell
python -m scripts.testing.cli.run_atomicbot --mode retest -- --matrix experiments/manifests/atomicbot-turboquant/retest-matrix.json --dry-run --only AB-01
python -m scripts.testing.cli.run_animehacker --mode quality -- --matrix experiments/manifests/animehacker-tq3-0/retest-matrix.json --prompt-set experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json --runtime-root experiments/raw-results/retained/animehacker-tq3-0/2026-07-18/runtime --output-root tmp/animehacker-quality
python -m scripts.testing.cli.run_openvino --mode format-boundary -- --status
python -m scripts.testing.cli.build_results --route all --output-root tmp/final-results-build
python -m scripts.testing.cli.validate_results --route all --output-root docs/testing/final-results
```

<!-- BEGIN BEGINNER DIRECTORY GUIDE -->

## Beginner directory guide

This is the supported code area for running, processing and validating tests.

### Start here

Start with the supported commands in [`cli/`](cli/README.md). Use lower-level modules only when you are maintaining the test system.

### How this folder fits into testing

This folder belongs to the tooling layer. It helps create or check evidence but is not evidence by itself.

### Folders

| Folder | What it contains |
| --- | --- |
| [`campaigns/`](campaigns/README.md) | Implements route-specific test campaign logic behind the supported command line tools. |
| [`cli/`](cli/README.md) | Provides the supported command line entry points for running and validating tests. |
| [`examples/`](examples/README.md) | Contains example input files that show the expected structure without being real results. |
| [`reporting/`](reporting/README.md) | Builds and validates the common Markdown, DOCX, PDF and data reports. |
| [`tests/`](tests/README.md) | Tests the testing tools themselves. These are not model benchmark results. |
| [`tools/`](tools/README.md) | Contains lower-level migration, capture, conversion, reconciliation and validation utilities. |
| [`turbovec/`](turbovec/README.md) | This folder covers the TurboVec feasibility experiment. |
| [`workbook05/`](workbook05/README.md) | Contains controlled helpers for Workbook 05 source admission, build and measurement stages. |

### Generated child folders

**Python cache folders:** 1 folder(s), for example `__pycache__/`. These are temporary Python bytecode caches. They are not source files or test evidence and do not receive README files.

### Files

| File | What it is for | Status and editing guidance |
| --- | --- | --- |
| [`Invoke-TurboVecFeasibility.ps1`](Invoke-TurboVecFeasibility.ps1) | Runs the Invoke TurboVecFeasibility PowerShell workflow. | Executable; may create or change outputs |
| [`requirements.txt`](requirements.txt) | Pinned or bounded Python packages needed by this testing area. | Supporting repository file |
| [`run_turbovec_feasibility.py`](run_turbovec_feasibility.py) | CLI entry point for the controlled TurboVec feasibility campaign. | Executable; may create or change outputs |
| [`Test-TurboVecEvidence.ps1`](Test-TurboVecEvidence.ps1) | Checks the Test TurboVecEvidence PowerShell workflow. | Executable or importable tooling |
| [`Validate-Workbook05-BuildStage.ps1`](Validate-Workbook05-BuildStage.ps1) | Runs the complete repository-side Workbook 05 documented-build gate. | Executable or importable tooling |
| [`Validate-Workbook05-MemoryFrontier.ps1`](Validate-Workbook05-MemoryFrontier.ps1) | Runs every repository-controlled Workbook 05 memory-frontier scaffold gate. | Executable or importable tooling |
| [`Validate-Workbook05-Phase3.ps1`](Validate-Workbook05-Phase3.ps1) | Checks the Validate Workbook05 Phase3 PowerShell workflow. | Executable or importable tooling |
| [`Validate-Workbook05-SourceAdmission.ps1`](Validate-Workbook05-SourceAdmission.ps1) | Runs every repository-controlled Workbook 05 Phase 1 source-admission gate. | Executable or importable tooling |

### Important boundaries

- Read command help before running a script. Commands named run, build, generate, convert, publish or finalise may create or change outputs.
- Passing tests for this tooling does not mean a model benchmark or application evaluation passed.

### Related guides

- [Parent guide](../README.md)
- [campaigns guide](campaigns/README.md)
- [cli guide](cli/README.md)
- [examples guide](examples/README.md)
- [reporting guide](reporting/README.md)
- [tests guide](tests/README.md)
- [tools guide](tools/README.md)
- [turbovec guide](turbovec/README.md)
- [workbook05 guide](workbook05/README.md)

<!-- END BEGINNER DIRECTORY GUIDE -->
