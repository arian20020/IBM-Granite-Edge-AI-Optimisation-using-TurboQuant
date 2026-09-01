# Testing Commands

`scripts/testing` now exposes the supported contributor-facing command surface for testing campaigns and final-results validation. The CLI layer stays thin: it dispatches to the proven campaign and reporting implementations without duplicating route logic.

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
python -m scripts.testing.cli.run_animehacker --mode quality -- --matrix experiments/manifests/animehacker-tq3-0/retest-matrix.json --prompt-set experiments/granite_turboquant_intel/prompts/fixed-feasibility-prompt-set-v1.json --runtime-root experiments/raw-results/animehacker-tq3-0/2026-07-18/runtime --output-root tmp/animehacker-quality
python -m scripts.testing.cli.run_openvino --mode format-boundary -- --status
python -m scripts.testing.cli.build_results --route all --output-root tmp/final-results-build
python -m scripts.testing.cli.validate_results --route all --output-root docs/testing/final-results
```
