# Reproducing the release checks

These commands validate and test the already-normalized release. The process does not rerun benchmarks and does not modify raw evidence. Run them from the repository root with a Python environment containing the project test dependencies, PyMuPDF, and openpyxl.

## Validate the collection read-only

```powershell
python scripts/testing/build_final_results.py --route all --output-root docs/testing/final-results --validate-only
```

`--validate-only` reads the route packages and release metadata. It does not publish receipts or regenerate DOCX, PDF, XLSX, CSV, or source evidence.

## Run the release tests

```powershell
python -m pytest scripts/testing/tests/test_final_results_*.py
```

PowerShell does not expand this wildcard for every executable. If your pytest build does not collect it, enumerate the matching paths first:

```powershell
$releaseTests = Get-ChildItem scripts/testing/tests/test_final_results_*.py | ForEach-Object FullName
python -m pytest @releaseTests
```

## Route-specific regeneration boundaries

Each route retains its own evidence-specific instructions. Those guides distinguish deterministic normalization/report regeneration from benchmark execution:

- [Upstream llama.cpp reproduction](01-upstream-llama-cpp/reproduction/README.md) and [commands](01-upstream-llama-cpp/reproduction/commands.md)
- [AtomicBot TurboQuant reproduction](02-atomicbot-turboquant/reproduction/README.md) and [commands](02-atomicbot-turboquant/reproduction/commands.md)
- [animehacker tq3-0 reproduction](03-animehacker-tq3-0/reproduction/README.md) and [commands](03-animehacker-tq3-0/reproduction/commands.md)
- [Experimental OpenVINO reproduction](04-openvino-experimental-fork/reproduction/README.md)
- [Official upstream OpenVINO reproduction](05-openvino-official-upstream/reproduction/README.md)
- [Cross-route reproduction](06-cross-route-comparison/reproduction/README.md) and [commands](06-cross-route-comparison/reproduction/commands.md)

Do not infer that regeneration fills unavailable model artifacts, re-executes failed conversions, or makes unmatched route dimensions comparable. The canonical CSVs and linked source evidence remain the authority.
