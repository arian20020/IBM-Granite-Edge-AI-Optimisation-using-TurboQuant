# Reproducing the release checks

These commands validate and test the already-normalized release. The process does not rerun benchmarks or quality adjudication and does not modify raw evidence. Run them from the repository root with a Python environment containing the project test dependencies, PyMuPDF, and openpyxl.

## Validate the collection read-only

```powershell
python -m scripts.testing.cli.build_results --route all --output-root docs/testing/final-results --validate-only
```

`--validate-only` reads the six compact route packages and release metadata. It does not publish receipts or regenerate DOCX, PDF, XLSX, CSV, or source evidence.

## Validate a self-contained Git archive

No external hydration is required. Replace `<release-commit>` with the enclosing commit, then validate only the bytes emitted by Git:

```powershell
git archive --format=tar --output=unified-final-results-v2.tar <release-commit>
New-Item -ItemType Directory archive-check | Out-Null
tar -xf unified-final-results-v2.tar -C archive-check
Set-Location archive-check
python -m scripts.testing.cli.build_results --route all --output-root docs/testing/final-results --validate-only
```

The archive already contains every source path cited by the five evidence indexes. Do not copy files into the extracted tree before validation; doing so would test a different release.

## Run the release tests

```powershell
python -m pytest scripts/testing/tests/integration/test_final_results_*.py scripts/testing/tests/acceptance/test_build_final_results.py scripts/testing/tests/acceptance/test_final_results_*.py scripts/testing/tests/acceptance/test_official_openvino_docx.py
```

PowerShell does not expand these wildcards for every executable. If your pytest build does not collect them, enumerate the matching paths first:

```powershell
$releaseTests =
  (Get-ChildItem scripts/testing/tests/integration/test_final_results_*.py | ForEach-Object FullName) +
  (Get-ChildItem scripts/testing/tests/acceptance/test_final_results_*.py | ForEach-Object FullName) +
  (Resolve-Path scripts/testing/tests/acceptance/test_build_final_results.py).Path +
  (Resolve-Path scripts/testing/tests/acceptance/test_official_openvino_docx.py).Path
python -m pytest @releaseTests
```

## OpenVINO workbook boundary

The originals under each OpenVINO route's `evidence/source/` directory are immutable evidence-only, nonportable XLSX files. The primary handoff workbooks under `reports/` replace machine-specific absolute path cells and hyperlink targets with stable repository-relative references while preserving scientific values, formulas, and sheet structure. Collection validation is read-only; it never rewrites either workbook.

The experimental workbook has 275 formulas and no cached formula results. It may show blank formula cells in a non-calculating reader until Excel recalculation; this is a display limitation, not formula removal.

## Route-specific boundaries

Each route retains evidence-specific context, but its executable validation command uses the supported `scripts.testing.cli` surface:

- [Upstream llama.cpp reproduction](01-upstream-llama-cpp/reproduction/README.md) and [commands](01-upstream-llama-cpp/reproduction/commands.md)
- [AtomicBot TurboQuant reproduction](02-atomicbot-turboquant/reproduction/README.md) and [commands](02-atomicbot-turboquant/reproduction/commands.md)
- [animehacker tq3-0 reproduction](03-animehacker-tq3-0/reproduction/README.md) and [commands](03-animehacker-tq3-0/reproduction/commands.md)
- [Experimental OpenVINO reproduction](04-openvino-experimental-fork/reproduction/README.md)
- [Official upstream OpenVINO reproduction](05-openvino-official-upstream/reproduction/README.md)
- [Cross-route reproduction](06-cross-route-comparison/reproduction/README.md) and [commands](06-cross-route-comparison/reproduction/commands.md)

Do not infer that validation fills unavailable model artifacts, re-executes failed conversions, reruns inference or quality scoring, or makes unmatched route dimensions comparable. The canonical CSVs and linked source evidence remain the authority.
