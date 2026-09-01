# Ordered reproduction commands

1. Normalize and render
```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.reporting.llama_adapter import write_atomicbot_route; write_atomicbot_route(root)"
```
2. Export the owned Word PDF
```powershell
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/testing/cli/export_report.ps1 -DocxPath docs/testing/final-results/02-atomicbot-turboquant/reports/atomicbot-turboquant-report.docx -PdfPath docs/testing/final-results/02-atomicbot-turboquant/reports/atomicbot-turboquant-report.pdf -TimeoutSeconds 180
```
3. Finalize and validate the PDF
```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.reporting.llama_adapter import finalize_atomicbot_route; finalize_atomicbot_route(root)"
```
4. Validate manifest
```powershell
& .tools/python311-portable/python.exe -c "import sys; from pathlib import Path; root=Path.cwd(); sys.path.insert(0,str(root)); from scripts.testing.reporting.evidence import validate_sha256_manifest; errors=validate_sha256_manifest(root,root/'docs/testing/final-results/02-atomicbot-turboquant/evidence/manifest-sha256.txt'); print(errors); raise SystemExit(bool(errors))"
```
5. Run focused validation
```powershell
& .tools/python311-portable/python.exe -m pytest scripts/testing/tests/integration/test_final_results_atomicbot.py -q
```
