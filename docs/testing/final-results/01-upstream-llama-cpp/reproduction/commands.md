# Ordered reproduction commands

Run from the repository root in PowerShell. Stop immediately if any command exits nonzero.

1. Normalize and render

```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.reporting.llama_adapter import write_upstream_llama_route; write_upstream_llama_route(root)"
```

2. Export the owned Word PDF

```powershell
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/testing/Export-Final-Results-Pdf.ps1 -DocxPath docs/testing/final-results/01-upstream-llama-cpp/workbook/generated/upstream-llama-cpp-final-report.docx -PdfPath docs/testing/final-results/01-upstream-llama-cpp/workbook/generated/upstream-llama-cpp-final-report.pdf -TimeoutSeconds 180
```

3. Finalize and validate the PDF

```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.reporting.llama_adapter import finalize_upstream_llama_route; finalize_upstream_llama_route(root)"
```

4. Validate the checksum manifest

```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.reporting.evidence import validate_sha256_manifest; errors=validate_sha256_manifest(root, root/'docs/testing/final-results/01-upstream-llama-cpp/evidence/manifest-sha256.txt'); print(errors); raise SystemExit(bool(errors))"
```

5. Run the focused validation suite

```powershell
& .tools/python311-portable/python.exe -m pytest scripts/testing/tests/test_final_results_upstream_llama.py -q
```
