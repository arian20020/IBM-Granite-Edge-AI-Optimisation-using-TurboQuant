# Reproducing the release checks

These commands validate and test the already-normalized release. The process does not rerun benchmarks and does not modify raw evidence. Run them from the repository root with a Python environment containing the project test dependencies, PyMuPDF, and openpyxl.

## Validate the collection read-only

```powershell
python scripts/testing/build_final_results.py --route all --output-root docs/testing/final-results --validate-only
```

`--validate-only` reads the route packages and release metadata. It does not publish receipts or regenerate DOCX, PDF, XLSX, CSV, or source evidence.

## Validate a self-contained Git archive

No external hydration is required. Replace `<release-commit>` with the enclosing commit, then validate only the bytes emitted by Git:

```powershell
git archive --format=tar --output=unified-final-results-v2.tar <release-commit>
New-Item -ItemType Directory archive-check | Out-Null
tar -xf unified-final-results-v2.tar -C archive-check
Set-Location archive-check
python scripts/testing/build_final_results.py --route all --output-root docs/testing/final-results --validate-only
```

The archive already contains every source path cited by the five evidence indexes. Do not copy files into the extracted tree before validation; doing so would test a different release.

## Run the release tests

```powershell
python -m pytest scripts/testing/tests/test_final_results_*.py
```

PowerShell does not expand this wildcard for every executable. If your pytest build does not collect it, enumerate the matching paths first:

```powershell
$releaseTests = Get-ChildItem scripts/testing/tests/test_final_results_*.py | ForEach-Object FullName
python -m pytest @releaseTests
```

## Regenerate portable OpenVINO workbooks

The originals under each OpenVINO route's `results/source/` directory are immutable evidence-only, nonportable XLSX files. The primary handoff workbooks under `workbook/generated/` replace machine-specific absolute path cells and hyperlink targets with stable repository-relative references while preserving scientific values, formulas, and sheet structure:

```powershell
python -c "from pathlib import Path; from scripts.testing.final_results.workbook_portability import write_portable_openvino_workbooks; write_portable_openvino_workbooks(Path.cwd())"
```

The experimental workbook has 275 formulas and no cached formula results. It may show blank formula cells in a non-calculating reader until Excel recalculation; this is a display limitation, not formula removal.

## Route-specific regeneration boundaries

Each route retains its own evidence-specific instructions. Those guides distinguish deterministic normalization/report regeneration from benchmark execution:

- [Upstream llama.cpp reproduction](01-upstream-llama-cpp/reproduction/README.md) and [commands](01-upstream-llama-cpp/reproduction/commands.md)
- [AtomicBot TurboQuant reproduction](02-atomicbot-turboquant/reproduction/README.md) and [commands](02-atomicbot-turboquant/reproduction/commands.md)
- [animehacker tq3-0 reproduction](03-animehacker-tq3-0/reproduction/README.md) and [commands](03-animehacker-tq3-0/reproduction/commands.md)
- [Experimental OpenVINO reproduction](04-openvino-experimental-fork/reproduction/README.md)
- [Official upstream OpenVINO reproduction](05-openvino-official-upstream/reproduction/README.md)
- [Cross-route reproduction](06-cross-route-comparison/reproduction/README.md) and [commands](06-cross-route-comparison/reproduction/commands.md)

Do not infer that regeneration fills unavailable model artifacts, re-executes failed conversions, or makes unmatched route dimensions comparable. The canonical CSVs and linked source evidence remain the authority.
